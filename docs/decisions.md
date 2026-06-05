# Decisões Técnicas

Registro das decisões de arquitetura relevantes do Order Service. Foco em **por quê** — o código já documenta o **o quê**.

---

## ADR-001 — Stack

Uma biblioteca por finalidade. Qualquer adição nova exige justificativa explícita.

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

Por request: `SET LOCAL app.current_customer_id / app.is_admin` dentro da transação (escopo transacional — sem vazamento no pool de conexões). A aplicação conecta com role `orders_app` sem permissão de owner.

---

## ADR-005 — Refresh token + revogação

Problema: access token de 60 min tem janela de abuso inaceitável para produção.

**Solução sem nova infra** (Postgres já existe):

| | Decisão | Descartado |
|---|---|---|
| Access token | 15 min + claim `jti` | 60 min |
| Refresh token | 7 dias, rotação a cada uso | 30 dias sem rotação |
| Transporte | Cookie `HttpOnly + Secure + SameSite=Strict` | Body / localStorage (XSS rouba) |
| Blacklist | Tabela `revoked_tokens` no Postgres | Redis (nova dependência) |
| Replay | Token já usado → revoga família inteira | Ignorar |

**Cookie HttpOnly:** JS não consegue ler → XSS rouba o access token (15 min de janela) mas não consegue renovar a sessão. SameSite=Strict bloqueia CSRF.

**Blacklist Postgres:** +1 query por request autenticada (`SELECT` em `revoked_tokens` com índice em `jti`). Custo aceito. Migrar para Redis quando a latência aparecer em profiling.

**Limpeza periódica necessária em produção:**
```sql
DELETE FROM revoked_tokens  WHERE expires_at < now();
DELETE FROM refresh_tokens  WHERE expires_at < now() AND revoked_at IS NOT NULL;
```
