# Decisões Técnicas

Registro das decisões de arquitetura relevantes do Order Service. Foco em **por quê** — o código já documenta o **o quê**.

---

## ADR-001 — Stack

| Finalidade | Escolha | Por quê não a alternativa |
|---|---|---|
| Runtime | .NET 8 / C# 12 | LTS; exigência da spec |
| API | Web API (Controllers) | Atributos de rota/auth, testabilidade com WebApplicationFactory |
| ORM | EF Core 8 + Npgsql | Exigência da spec; migrações gerenciadas |
| Auth | JWT Bearer | Exigência da spec |
| Validação de request | FluentValidation | Declarativa, testável isoladamente; Guards ficam no domínio |
| Mapeamento | Manual | Auditável; sem mapeamento implícito que esconde bugs |
| Casos de uso | Application Services | Sem MediatR — dependências explícitas, menos indireção |
| Testes | xUnit + Moq + FluentAssertions | Padrão .NET; sem mistura de frameworks |
| Integração | Testcontainers | Postgres real e descartável; sem mock de banco |
| Logging | ILogger nativo | Sem dependência extra |
| Docs | Swashbuckle | Padrão; zero config adicional |
| Execução | Docker local | Sem SDK no host; reprodutível em qualquer máquina |

---

## ADR-002 — Ciclo de vida do pedido

Pedido **nasce `Placed`** — sem estado `Draft` intermediário. Estoque é baixado no `Confirm`, devolvido no `Cancel`. Ambos idempotentes.

Alternativa descartada: `Draft → Placed` com reserva na criação. Adiciona estado extra e exige compensação em caso de abandono.

---

## ADR-003 — Estoque nunca negativo sob concorrência

Três camadas de proteção:

1. **Decremento atômico:** `UPDATE products SET available_quantity = available_quantity - @qty WHERE id = @id AND available_quantity >= @qty` — 0 linhas afetadas = estoque insuficiente, sem lock.
2. **CHECK de schema:** `CHECK (available_quantity >= 0)` — última barreira, nunca deve disparar se o UPDATE estiver correto.
3. **Teste de race condition obrigatório:** estoque `N`, `M > N` goroutines concorrentes → exatamente `N` sucessos, `available_quantity` final `== 0`.

`xmin` no agregado `Order` para concorrência otimista em edições do pedido (`DbUpdateConcurrencyException` → HTTP 409).

---

## ADR-004 — RLS no banco

Defesa em profundidade além do JWT. Mesmo com bug de autorização na aplicação, o banco isola os dados por cliente.

Implementação: `ENABLE ROW LEVEL SECURITY` + `FORCE ROW LEVEL SECURITY` nas tabelas de pedidos. Política: `customer_id = current_setting('app.current_customer_id')::uuid OR current_setting('app.is_admin') = 'true'`.

Por request, dentro da transação: `set_config('app.current_customer_id', $1, true)` e `set_config('app.is_admin', $2, true)` — **parametrizado** (sem interpolação de string), eliminando o vetor de injeção SQL justamente na fronteira do isolamento por tenant. O `true` final dá escopo transacional (sem vazamento no pool de conexões). A aplicação conecta com a role `orders_app`, sem permissão de owner.

**Consequência de contrato:** com o RLS enforced (runtime como `orders_app`), o pedido de outro cliente fica **invisível** — a query não retorna a linha. Logo, acesso cross-tenant resulta em **404 Not Found** (não 403 Forbidden), o que é mais seguro por não revelar a existência do recurso. O check de posse na camada de aplicação (que retornaria 403) permanece como defesa redundante, mas na prática o RLS filtra antes.

---

## ADR-005 — Refresh token + revogação

**Solução sem nova infra** (Postgres já existe. Ideia seria usar Redis, mas preferi usar apenas o postgreSQL por se tratar de um projeto "testing".):

| | Decisão |
|---|---|
| Access token | 15 min + claim `jti` |
| Refresh token | 7 dias, rotação a cada uso |
| Transporte | Cookie `HttpOnly + Secure + SameSite=Strict` |
| Blacklist | Tabela `revoked_tokens` no Postgres |
| Replay | Token já usado → revoga família inteira |

**Cookie HttpOnly:** JS não consegue ler → XSS rouba o access token (15 min de janela) mas não consegue renovar a sessão. SameSite=Strict bloqueia CSRF.

**Blacklist Postgres:** +1 query por request autenticada (`SELECT` em `revoked_tokens` com índice em `jti`). Custo aceito. Migrar para Redis quando a latência aparecer em profiling.

**Limpeza automática:** `ExpiredTokenCleanupService` (`BackgroundService`) roda a cada hora e apaga registros expirados das duas tabelas. Não requer agendamento externo.

---

## ADR-006 — Estratégia Docker e migrations automáticas

### Migrations no startup (`MigrateAsync()` em `Program.cs`)

Escolhido em vez de step separado (init container ou script de entrypoint). A API só inicia após o healthcheck do Postgres passar (`depends_on: condition: service_healthy`), eliminando o risco de corrida. Para projetos maiores com múltiplas instâncias em deploy paralelo, considerar lock distribuído ou migration em pipeline separado.

### Duas conexões: migração (superuser) × runtime (`orders_app`)

As migrations incluem DDL privilegiado: `CREATE ROLE orders_app`, `ALTER TABLE ... ENABLE ROW LEVEL SECURITY`, `GRANT`. Essas operações exigem `SUPERUSER` ou `CREATEROLE`. Por isso o compose define **duas** connection strings: `MigrationConnection` (superuser `postgres`, usada só pelo `MigrateAsync` no startup) e `DefaultConnection` (role restrita `orders_app`, usada pelo runtime). Assim o RLS fica **enforced em produção** sem abrir mão das migrations automáticas. A migration `GrantTokenTablesToAppRole` concede a `orders_app` o DML em `refresh_tokens`/`revoked_tokens` (criadas depois do `GRANT` inicial), necessário para os fluxos de auth.

### RLS bypass apenas nos testes

Conectar como superuser contorna o RLS — superuser ignora políticas independente de `FORCE ROW LEVEL SECURITY`. A suíte de integração padrão (`OrderApiFactory`) conecta como superuser de propósito, para semear/inspecionar dados livremente; ali o isolamento exercitado é o da camada de aplicação. Já o RLS **enforced** (cenário de produção) é validado por uma factory dedicada (`RlsApiFactory`) que conecta como `orders_app`: os testes de `AccessControlTests` rodam sob ela e comprovam o isolamento real no banco (acesso cross-tenant → 404).

### Dockerfile multi-stage

Stage `sdk:8.0` para restore/build/publish, stage `aspnet:8.0` para runtime. Imagem final sem SDK (~230 MB vs ~800 MB). Usuário não-root (uid 1001) criado no stage runtime para reduzir superfície de ataque.

---

## ADR-007 — Value Objects para Money e Quantity

Primitivos (`decimal`, `int`) não carregam invariantes — a validação se espalha pelos serviços (Primitive Obsession). `Money` (amount + currency) e `Quantity` (>0) encapsulam suas regras no construtor: `Money.Of` exige moeda ISO-4217 de 3 caracteres e amount ≥ 0; soma de moedas diferentes lança `DomainException`. `Quantity.Of` exige > 0. `Order.Total` retorna `Money` (soma dos itens). `OrderItem.UnitPrice` é `Money`, mapeado por `OwnsOne` (colunas `unit_price` + `unit_price_currency` — ver migration `0007`); `Quantity` usa value converter para a coluna `integer`. `Money` permite amount 0 para servir de identidade em somas de agregado vazio; `OrderItem.Create` valida amount > 0 à parte.

---

