# 0001 — InitialCreate

`20260605160035_InitialCreate.cs`

## Schema

**`orders`** — `xmin` como token de concorrência otimista (coluna de sistema do Postgres, sem coluna extra). EF mapeia como `rowVersion`.

**`order_items`** — FK `order_id → orders.id` CASCADE DELETE. Shadow property gerenciada pelo EF; o agregado expõe `IReadOnlyCollection<OrderItem>`.

**`products`** — `CHECK (available_quantity >= 0)` como última barreira contra estoque negativo. O decremento atômico no UPDATE deveria impedir antes, mas o CHECK garante consistência mesmo em acesso direto ao banco.

Índices em `orders`: `customer_id`, `status`, `created_at` — cobrem os três filtros do `GET /orders`.

## Segurança

Role `orders_app` criada com `IF NOT EXISTS` (idempotente). Tem `SELECT/INSERT/UPDATE/DELETE` nas três tabelas, sem DDL — não consegue alterar schema.

RLS em `orders` e `order_items` com `FORCE ROW LEVEL SECURITY` (força mesmo para o owner da tabela). Política: `customer_id = current_setting('app.current_customer_id')::uuid OR current_setting('app.is_admin') = 'true'`. Variáveis definidas via `SET LOCAL` dentro de cada transação pelo `RlsInterceptor` — escopo transacional, sem vazamento no pool.

## Trade-offs

- `xmin` não aparece no `SELECT *` — EF precisa de `HasColumnType("xid").IsRowVersion()`. Vantagem: zero coluna extra, Postgres gerencia automaticamente. Desvantagem: não portável para outros bancos.
- `FORCE RLS` significa que até queries de migrations/seeds precisam do contexto de sessão correto ou rodar com role diferente (sem RLS). Por isso a `DesignTimeDbContextFactory` usa uma string de conexão separada.
- `Total` do pedido é derivado (`SUM`) — ignorado no mapeamento EF, calculado em memória ao materializar o agregado.
