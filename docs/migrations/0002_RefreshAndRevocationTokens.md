# Migration 0002 — RefreshAndRevocationTokens

**Arquivo EF:** `20260605203137_RefreshAndRevocationTokens.cs`
**Data:** 2026-06-05
**Autor:** ThiagoDev202

## Objetivo

Suportar refresh tokens com rotação (ADR-0005) e revogação de access tokens por logout (blacklist de JTIs).

## Tabelas criadas

### `refresh_tokens`

Armazena as credenciais de sessão de longa duração. O valor bruto do token **nunca** é armazenado — apenas o hash SHA-256.

| Coluna | Tipo | Descrição |
|---|---|---|
| `id` | `uuid` PK | Identificador interno |
| `customer_id` | `uuid` NOT NULL | Dono do token |
| `token_hash` | `text(128)` NOT NULL UNIQUE | SHA-256 do token bruto (hex lowercase) |
| `expires_at` | `timestamptz` NOT NULL | Quando expira |
| `revoked_at` | `timestamptz` NULL | NULL = ativo; preenchido ao revogar |
| `replaced_by` | `text(128)` NULL | Hash do token que substituiu (rotação) |
| `created_at` | `timestamptz` NOT NULL | Emissão |

**Índices:** `ix_refresh_tokens_customer` (customer_id), `ix_refresh_tokens_expires` (expires_at), UNIQUE em `token_hash`.

### `revoked_tokens`

Blacklist de JTIs de access tokens revogados por logout. Registro removível após `expires_at`.

| Coluna | Tipo | Descrição |
|---|---|---|
| `jti` | `text(128)` PK | Claim `jti` do JWT revogado |
| `expires_at` | `timestamptz` NOT NULL | Quando o JWT original expiraria |
| `revoked_at` | `timestamptz` NOT NULL | Quando foi revogado |

**Índices:** `ix_revoked_tokens_expires` (expires_at) — permite cleanup de registros expirados com `DELETE WHERE expires_at < now()`.

## Notas operacionais

- **Cleanup:** Agendar `DELETE FROM revoked_tokens WHERE expires_at < now()` periodicamente (ex.: diário).
- **Cleanup refresh_tokens:** `DELETE FROM refresh_tokens WHERE expires_at < now() AND revoked_at IS NOT NULL`.
- **Migração automática:** aplicada no startup da API via `database.MigrateAsync()` (Fase 6).
- **Rollback:** `Down()` remove as duas tabelas — sem impacto em `orders`, `order_items`, `products`.
