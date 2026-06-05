# Order Service — API REST de Pedidos

API REST para gestão de **Pedidos** com itens e validação de estoque, construída em **.NET 8** seguindo **Clean Architecture**, **DDD** e **SOLID**. Persistência com **EF Core 8 + PostgreSQL**, segurança via **JWT** (reforçada por **RLS** no banco), e execução completa em **Docker**.

> Status: em construção (ver roadmap por fases). Tudo roda em **Docker local** — não é necessário instalar o .NET SDK no host.

---

## Stack

| Finalidade | Tecnologia |
|---|---|
| Runtime / Linguagem | .NET 8 / C# 12 |
| API | ASP.NET Core Web API (Controllers) |
| ORM | EF Core 8 + Npgsql + migrations |
| Banco | PostgreSQL |
| Autenticação | JWT Bearer |
| Validação | FluentValidation + invariantes de domínio (Guards) |
| Testes | xUnit + Moq + FluentAssertions + Testcontainers |
| Docs de API | Swagger / OpenAPI |

## Arquitetura

```
src/
  OrderService.Domain/          # Entidades, Value Objects, invariantes (DDD). Sem dependências externas.
  OrderService.Application/     # Casos de uso, DTOs, validações, abstrações.
  OrderService.Infrastructure/  # EF Core, repositórios, migrations, JWT, concorrência, RLS.
  OrderService.Api/             # Controllers, composição (DI), middleware, Swagger.
tests/
  OrderService.UnitTests/       # Testes de domínio e casos de uso.
  OrderService.IntegrationTests/# Testes end-to-end (Postgres real via Testcontainers).
```

## Endpoints (alvo)

| Método | Rota | Descrição |
|---|---|---|
| POST | `/auth/token` | Emite JWT |
| POST | `/orders` | Cria pedido (valida estoque; nasce `Placed`) |
| POST | `/orders/{id}/confirm` | Confirma e baixa estoque (idempotente) |
| POST | `/orders/{id}/cancel` | Cancela e devolve estoque (idempotente) |
| GET | `/orders/{id}` | Consulta pedido + itens |
| GET | `/orders` | Lista com paginação e filtros (`customerId`, `status`, `from`, `to`, `page`, `pageSize`) |

## Como rodar

> Pré-requisito: **Docker**.

### Build e testes (imagem SDK)

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 dotnet build -warnaserror
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 dotnet test
```

### API + banco (runtime)

```bash
docker compose up --build
```

A API sobe junto com o PostgreSQL e aplica as migrations automaticamente. A documentação Swagger fica disponível em `/swagger`.

> _docker-compose, portas e variáveis de ambiente serão definidos na Fase 6 do roadmap._

## Decisões técnicas

Registradas em [docs/decisions.md](docs/decisions.md).
