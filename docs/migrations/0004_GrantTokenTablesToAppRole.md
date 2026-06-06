# 0004 — GrantTokenTablesToAppRole

`20260606154430_GrantTokenTablesToAppRole.cs`

## Schema

Sem mudança de schema — apenas `GRANT SELECT, INSERT, UPDATE, DELETE ON refresh_tokens, revoked_tokens TO orders_app` (e o `REVOKE` correspondente no `Down`).

## Por quê

`refresh_tokens` e `revoked_tokens` foram criadas na migration `0002`, **depois** do `GRANT` inicial concedido a `orders_app` na `0001` (que cobria apenas `orders`, `order_items`, `products`). Resultado: a role de runtime não tinha acesso às tabelas de token.

Enquanto o runtime conectava como superuser, o problema ficava latente. Ao passar o runtime para a role restrita `orders_app` (RLS enforced — ver ADR-006), conectar sem este `GRANT` quebraria os fluxos de `auth/token`, `auth/refresh` e `auth/logout` com *permission denied*.

## Observação

As tabelas de token **não** têm RLS (não são escopadas por cliente da mesma forma que pedidos), então só precisam do `GRANT` de DML — nenhuma política adicional.
