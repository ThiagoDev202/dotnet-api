# 0005 — ParametrizeSenhaOrdersApp

`20260607000001_ParametrizeSenhaOrdersApp.cs`

## Schema

Sem mudança de schema. Executa um bloco `DO $$ ... $$` que lê
`current_setting('app.orders_app_password', true)` e, se a variável estiver
definida e não vazia, roda `ALTER ROLE orders_app PASSWORD` via `format(..., %L)`
(escape correto, sem interpolação). O `Down` não reverte — a senha anterior é
desconhecida.

## Por quê

A `0001` criava `orders_app` com senha hardcoded `'orders_app_password'`. Isso
deixava a credencial de runtime fixa no código e desalinhada do `.env`. A migration
passa a aplicar a senha vinda do ambiente (variável de sessão `app.orders_app_password`),
parametrizada para eliminar injeção.

## Observação

Pareia com a sincronização no startup (ver ADR-006): o `Program.cs` faz
`set_config('app.orders_app_password', <APP_DB_PASSWORD>, false)` antes do
`MigrateAsync`, garantindo que a role fique com a senha do `.env` mesmo quando o
volume já existe. Migration escrita à mão → exige os atributos `[DbContext]` e
`[Migration("...")]` no Designer para ser descoberta no startup.
