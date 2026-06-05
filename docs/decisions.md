# Decisões Técnicas (ADRs)

Registro das decisões de arquitetura e stack do Order Service. Cada decisão segue: contexto, decisão, consequências.

---

## ADR-0001 — Stack e arquitetura base

**Data:** 2026-06-05
**Status:** Aceita

### Contexto

Teste técnico .NET Sênior: API REST de Pedidos pronta para produção no essencial. A spec exige .NET 8+, EF Core + migrations, PostgreSQL, separação Domain/Application/Infrastructure/API, JWT, testes e `docker compose up`. Várias escolhas eram abertas ("xUnit ou NUnit", "Web API ou Minimal API"). Princípio do projeto: **uma única biblioteca por finalidade**.

### Decisão

| Finalidade | Escolha | Justificativa |
|---|---|---|
| Runtime / Linguagem | .NET 8 (LTS) / C# 12 | Exigência da spec; LTS |
| API | ASP.NET Core Web API (Controllers) | Organização por recurso, atributos de rota/auth, testabilidade |
| ORM | EF Core 8 + Npgsql + migrations | Exigência da spec |
| Banco | PostgreSQL | Exigência da spec |
| Auth | JWT Bearer | Exigência da spec |
| Validação de request | FluentValidation | Declarativa e testável; domínio mantém invariantes (Guards) |
| Mapeamento DTO↔domínio | Manual | Auditável; evita mapeamento implícito difícil de debugar |
| Casos de uso | Application Services explícitos (sem MediatR) | Menos indireção; dependências explícitas |
| Testes | xUnit | Padrão dominante no ecossistema .NET |
| Mocking | Moq | Maduro e amplamente conhecido |
| Asserções | FluentAssertions | Legibilidade e mensagens de falha claras |
| Integração (DB real) | Testcontainers for PostgreSQL | Testa contra Postgres real, descartável |
| Logging | Microsoft.Extensions.Logging | Nativo; evita dependência extra |
| Docs de API | Swashbuckle (Swagger) | Melhora a experiência de avaliar/rodar |
| Execução | Docker local | Sem SDK no host; reprodutível |

### Consequências

- Nenhuma duplicidade de ferramenta por finalidade.
- Build/testes rodam via imagem `mcr.microsoft.com/dotnet/sdk:8.0`; runtime via `docker compose`.

---

## ADR-0002 — Ciclo de vida do pedido e baixa de estoque

**Data:** 2026-06-05
**Status:** Aceita

### Contexto

O pedido pode nascer `Placed` ou `Draft→Placed`; a baixa de estoque pode ocorrer na criação ou na confirmação. É preciso evitar overselling sob concorrência.

### Decisão

- Pedido **nasce `Placed`** (valida existência de produto e disponibilidade, mas não reserva).
- **Confirm** baixa o estoque; **Cancel** devolve. Ambos **idempotentes** (2ª chamada = mesmo resultado).

### Consequências

- Fluxo simples e aderente aos MUST; menos estados intermediários.

---

## ADR-0003 — Concorrência e garantia de estoque não-negativo

**Data:** 2026-06-05
**Status:** Aceita

### Contexto

Confirmações concorrentes do mesmo produto não podem vender estoque inexistente. A spec valoriza performance — evitar locks longos.

### Decisão

- **Decremento atômico condicional:** `UPDATE stock SET available_quantity = available_quantity - @qty WHERE product_id = @id AND available_quantity >= @qty`. 0 linhas afetadas = estoque insuficiente → erro de negócio.
- **`CHECK (available_quantity >= 0)`** no schema como garantia final.
- **Concorrência otimista** no agregado `Order` via token `xmin` → `DbUpdateConcurrencyException` traduzida.
- **Teste de race condition obrigatório** (estoque N, M>N confirmações concorrentes → N sucessos, final == 0, nunca < 0).

### Consequências

- Sem overselling, sem locks pessimistas de longa duração; comprovado por teste automatizado.

---

## ADR-0004 — Segurança no banco com RLS (Row-Level Security)

**Data:** 2026-06-05
**Status:** Aceita

### Contexto

Além do JWT na aplicação, deseja-se defesa em profundidade no nível do banco para isolar pedidos por cliente.

### Decisão

- **RLS por cliente + bypass admin.** `ENABLE`/`FORCE ROW LEVEL SECURITY` nas tabelas de pedidos; política `customer_id = current_setting('app.current_customer_id')::uuid OR current_setting('app.is_admin') = 'true'`.
- Por requisição, `SET LOCAL app.current_customer_id`/`app.is_admin` a partir dos claims JWT (escopo transacional, sem vazar no pool).
- Aplicação conecta como role **sem owner** (`orders_app`); migrations rodam como role dono separado.

### Consequências

- Mesmo com bug de autorização na aplicação, o banco impede acesso cruzado entre clientes. Exige cuidado com a role de conexão e gestão da variável de sessão.
