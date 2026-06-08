# 0007 — AddCurrencyToOrderItem

`20260608000001_AddCurrencyToOrderItem.cs`

## Schema

Adiciona a coluna `unit_price_currency varchar(3) NOT NULL DEFAULT 'BRL'` em `order_items`.
O `Down` faz `DROP COLUMN`.

## Por quê

`OrderItem.UnitPrice` deixou de ser um `decimal` puro e passou a ser o Value Object `Money`
(amount + currency), mapeado via `OwnsOne`. Isso exige uma coluna para a moeda do preço
unitário ao lado de `unit_price`. O `DEFAULT 'BRL'` preenche as linhas existentes sem
exigir backfill manual.

## Observação

Migration escrita manualmente (sem `dotnet ef migrations add`). Por isso os atributos
`[DbContext(typeof(OrderServiceDbContext))]` e `[Migration("...")]` são **obrigatórios**:
o EF Core carrega o assembly de migrations via `Assembly.Load`, criando uma instância
distinta do assembly do contexto — sem os atributos a migration não é descoberta no startup.
