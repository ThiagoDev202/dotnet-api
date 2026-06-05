# 0001 — InitialCreate

**Arquivo:** `20260605160035_InitialCreate.cs`
**Fase:** 3 — Infrastructure

## Tabelas criadas

| Tabela | Descrição |
|--------|-----------|
| `orders` | Pedidos; `xmin` como token de concorrência otimista (sistema Postgres) |
| `order_items` | Itens do pedido; FK `order_id` → `orders.id` (CASCADE DELETE) |
| `products` | Catálogo/estoque; `CHECK (available_quantity >= 0)` impede estoque negativo no schema |

## Índices

| Índice | Tabela | Coluna | Finalidade |
|--------|--------|--------|------------|
| `ix_orders_customer_id` | `orders` | `customer_id` | Filtro `GET /orders?customerId=` |
| `ix_orders_status` | `orders` | `status` | Filtro `GET /orders?status=` |
| `ix_orders_created_at` | `orders` | `created_at` | Filtro `from`/`to` e ordenação |
| `ix_order_items_order_id` | `order_items` | `order_id` | JOIN/Include automático pelo EF |

## Segurança

### Role `orders_app`
- Criado com `IF NOT EXISTS` (idempotente).
- Permissões: `SELECT`, `INSERT`, `UPDATE`, `DELETE` em `orders`, `order_items`, `products`.
- Sem privilégios de DDL — não pode alterar schema.

### RLS — Row-Level Security

**`orders`:**
- `ENABLE ROW LEVEL SECURITY` + `FORCE ROW LEVEL SECURITY`
- Policy `customer_isolation`: `customer_id = current_setting('app.current_customer_id')` OR `is_admin = 'true'`

**`order_items`:**
- Mesma configuração; policy derivada dos pedidos do cliente via subquery em `orders`.

**Variáveis de sessão (SET LOCAL):** definidas pelo `RlsInterceptor` ao iniciar cada transação a partir dos claims JWT. `SET LOCAL` tem escopo de transação — elimina risco de vazamento no pool de conexões.

## Rollback (`Down`)

Remove as policies de RLS e desabilita `ROW LEVEL SECURITY` antes de dropar as tabelas.
