# 0006 — AddRoleToRefreshToken

`20260607000002_AddRoleToRefreshToken.cs`

## Schema

Adiciona `role varchar(20) NOT NULL DEFAULT 'Customer'` em `refresh_tokens`.
O `Down` faz `DROP COLUMN`.

## Por quê

Antes, todo refresh reemitia o access token com role fixa `'Customer'`, rebaixando
silenciosamente um Admin a cada renovação. Persistir a role no refresh token permite
que a rotação (`Replace`) preserve o nível de acesso por toda a vida da sessão. O
`DEFAULT 'Customer'` preenche linhas existentes sem backfill.

## Observação

Migration escrita à mão → atributos `[DbContext]` e `[Migration("...")]` obrigatórios
no Designer (mesmo motivo descrito na 0007).
