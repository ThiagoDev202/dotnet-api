# 0002 — RefreshAndRevocationTokens

`20260605203137_RefreshAndRevocationTokens.cs`

## Schema

**`refresh_tokens`** — credenciais de sessão de longa duração. Nunca armazena o valor bruto: `token_hash` é SHA-256 hex do token gerado. `replaced_by` guarda o hash do token que substituiu (rastro da cadeia de rotação). `revoked_at` NULL = ativo.

**`revoked_tokens`** — blacklist de JTIs de access tokens revogados por logout. `jti` é PK (claim do JWT). Registros podem ser apagados após `expires_at` sem perda de segurança — token expirado já seria rejeitado pela validação JWT antes de chegar ao middleware.

Índices em `expires_at` nas duas tabelas para facilitar o cleanup periódico.

## Trade-offs

**Postgres em vez de Redis para a blacklist.** Elimina nova dependência de infra. Custo: +1 query por request autenticada (`SELECT` por `jti` com índice PK). Para este serviço é aceitável. Se a latência aparecer em profiling, a interface `ITokenRevocationService` já isola a implementação — trocar por Redis é uma mudança só na Infrastructure.

**SHA-256 no hash do refresh token.** Não é bcrypt — refresh token tem 64 bytes de entropia (`RandomNumberGenerator`), o que torna brute-force computacionalmente inviável mesmo com hash rápido. bcrypt adicionaria latência desnecessária sem ganho real de segurança neste caso.

**`RevokedToken` como model de Infrastructure, não entity de Domain.** É um concern técnico de revogação, não regra de negócio. Não faz sentido o Domain conhecer blacklist de JTIs.

## Limpeza periódica (produção)

```sql
DELETE FROM revoked_tokens WHERE expires_at < now();
DELETE FROM refresh_tokens WHERE expires_at < now() AND revoked_at IS NOT NULL;
```

Sem limpeza, as tabelas crescem indefinidamente. Agendar diariamente via cron ou job interno.
