# Order Service — API REST de Pedidos

API REST para gestão de **Pedidos** com itens e validação de estoque, construída em **.NET 8** seguindo **Clean Architecture**, **DDD** e **SOLID**. Persistência com **EF Core 8 + PostgreSQL**, segurança via **JWT** (reforçada por **RLS** no banco), e execução completa em **Docker**.

> Pré-requisito único: **Docker**. Não é necessário instalar o .NET SDK no host.

---

## Stack

| Finalidade | Tecnologia |
|---|---|
| Runtime / Linguagem | .NET 8 / C# 12 |
| API | ASP.NET Core Web API (Controllers) |
| ORM | EF Core 8 + Npgsql + migrations |
| Banco | PostgreSQL 16 |
| Autenticação | JWT Bearer + Refresh Token (HttpOnly cookie) |
| Validação | FluentValidation + invariantes de domínio (Guards) |
| Testes | xUnit + Moq + FluentAssertions + Testcontainers |
| Docs de API | Swagger / OpenAPI |

---

## Arquitetura

```
src/
  OrderService.Domain/          # Entidades, Value Objects, invariantes (DDD). Sem dependências externas.
  OrderService.Application/     # Casos de uso, DTOs, validações, abstrações.
  OrderService.Infrastructure/  # EF Core, repositórios, migrations, JWT, concorrência, RLS.
  OrderService.Api/             # Controllers, composição (DI), middleware, Swagger.
tests/
  OrderService.UnitTests/       # Testes de domínio e casos de uso (xUnit + Moq).
  OrderService.IntegrationTests/# Testes end-to-end (Postgres real via Testcontainers).
```

Dependências apontam para dentro: `Domain` ← `Application` ← `Infrastructure` / `Api`.

---

## Endpoints

| Método | Rota | Auth | Descrição |
|---|---|---|---|
| POST | `/auth/token` | — | Emite access token (body) + refresh token (cookie HttpOnly) |
| POST | `/auth/refresh` | — | Renova access token via cookie; rotaciona refresh token |
| POST | `/auth/logout` | Bearer | Revoga jti (blacklist) + refresh token; apaga cookie |
| POST | `/orders` | Bearer | Cria pedido (valida estoque; nasce `Placed`) |
| POST | `/orders/{id}/confirm` | Bearer | Confirma e baixa estoque (idempotente) |
| POST | `/orders/{id}/cancel` | Bearer | Cancela e devolve estoque (idempotente) |
| GET | `/orders/{id}` | Bearer | Consulta pedido + itens |
| GET | `/orders` | Bearer | Lista com paginação e filtros (`customerId`, `status`, `from`, `to`, `page`, `pageSize`) |

---

## Como rodar

### 1. Configurar variáveis de ambiente

```bash
cp .env.example .env
```

Edite `.env` com seus valores:

```env
POSTGRES_PASSWORD=sua_senha_postgres
JWT_KEY=chave-jwt-minimo-32-caracteres-troque-aqui
```

### 2. Subir API + banco

```bash
docker compose up --build
```

- As migrations são aplicadas **automaticamente** no startup
- Swagger disponível em **http://localhost:8080/swagger**
- Health check em **http://localhost:8080/health**

### 3. Parar

```bash
docker compose down          # para e remove containers
docker compose down -v       # + apaga volume do Postgres
```

---

## Variáveis de ambiente

| Variável | Obrigatório | Default | Descrição |
|---|---|---|---|
| `JWT_KEY` | Sim | — | Chave secreta JWT (mínimo 32 chars) |
| `POSTGRES_PASSWORD` | Não | `postgres` | Senha do superuser Postgres |
| `Jwt__Issuer` | Não | `OrderService` | Issuer do token |
| `Jwt__Audience` | Não | `OrderServiceApi` | Audience do token |
| `ASPNETCORE_ENVIRONMENT` | Não | `Development` | Habilita Swagger quando `Development` |

---

## Exemplos de uso

Substitua `<TOKEN>` pelo access token obtido em `/auth/token`.

### Obter token

```bash
curl -c cookies.txt -X POST http://localhost:8080/auth/token \
  -H "Content-Type: application/json" \
  -d '{"customerId":"550e8400-e29b-41d4-a716-446655440000","role":"Customer"}'
```

Resposta:
```json
{
  "token": "eyJhbGci..."
}
```

O refresh token chega automaticamente no cookie `HttpOnly` (capturado pelo `-c cookies.txt`).

### Criar pedido

```bash
curl -X POST http://localhost:8080/orders \
  -H "Authorization: Bearer <TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{
    "items": [
      { "productId": "<PRODUCT_ID>", "quantity": 2 }
    ],
    "currency": "BRL"
  }'
```

### Confirmar pedido

```bash
curl -X POST http://localhost:8080/orders/<ORDER_ID>/confirm \
  -H "Authorization: Bearer <TOKEN>"
```

### Consultar pedido

```bash
curl http://localhost:8080/orders/<ORDER_ID> \
  -H "Authorization: Bearer <TOKEN>"
```

### Listar pedidos com filtros

```bash
curl "http://localhost:8080/orders?status=Confirmed&page=1&pageSize=10" \
  -H "Authorization: Bearer <TOKEN>"
```

### Renovar token

```bash
curl -b cookies.txt -c cookies.txt -X POST http://localhost:8080/auth/refresh \
  -H "Authorization: Bearer <TOKEN_EXPIRADO>"
```

### Logout

```bash
curl -b cookies.txt -X POST http://localhost:8080/auth/logout \
  -H "Authorization: Bearer <TOKEN>"
```

---

## Build e testes (desenvolvimento)

### Build

```bash
docker run --rm \
  --user "$(id -u):$(id -g)" \
  -e HOME=/tmp -e DOTNET_CLI_HOME=/tmp -e DOTNET_NOLOGO=1 \
  -v "$PWD":/src -w /src \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet build OrderService.sln -warnaserror
```

### Testes

Os testes de integração usam Testcontainers, que precisa acessar o socket do Docker:

```bash
DOCKER_GID=$(stat -c '%g' /var/run/docker.sock)

docker run --rm \
  --user "$(id -u):$(id -g)" \
  --group-add "$DOCKER_GID" \
  -e HOME=/tmp -e DOTNET_CLI_HOME=/tmp -e DOTNET_NOLOGO=1 \
  -e DOCKER_HOST=unix:///var/run/docker.sock \
  -v "$PWD":/src \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -w /src \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test OrderService.sln
```

Resultado esperado: **109/109 testes passando** (80 unitários + 29 integração, incluindo race condition de estoque).

---

## Segurança

- **JWT** com 15 min de validade + claim `jti` para revogação imediata no logout
- **Refresh token** de 7 dias em cookie `HttpOnly + Secure + SameSite=Strict`; rotação obrigatória a cada uso; replay detectado revoga a família inteira
- **Blacklist de JTIs** em Postgres (`revoked_tokens`), verificada a cada request autenticada
- **RLS** (Row-Level Security) no banco: clientes só enxergam seus próprios pedidos, mesmo em acesso direto ao banco
- **Rate limiting**: 100 req/min global, 10 req/min em `/auth` (proteção contra brute-force)
- **Security headers**: `X-Content-Type-Options`, `X-Frame-Options`, `X-XSS-Protection`, `Referrer-Policy`, `Permissions-Policy`
- **HSTS** (365 dias) em produção

---

## Decisões técnicas

Registradas em [docs/decisions.md](docs/decisions.md).
