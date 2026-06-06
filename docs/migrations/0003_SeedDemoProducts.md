# 0003 — SeedDemoProducts

`20260606154402_SeedDemoProducts.cs`

## Schema

Sem mudança de schema — apenas `INSERT` de dados de demonstração na tabela `products` via `HasData` em `ProductConfiguration`.

| id | name | unit_price | available_quantity |
|---|---|---|---|
| `11111111-1111-1111-1111-111111111111` | Caneta Azul | 10.00 | 100 |
| `22222222-2222-2222-2222-222222222222` | Caderno Universitário | 25.50 | 100 |

## Trade-offs

**Por que semear via migration.** Não há endpoint de cadastro de produto (fora do escopo da spec, que só exige endpoints de pedido + auth). Sem seed, o banco sobe vazio e é impossível criar um pedido logo após `docker compose up` — o `<PRODUCT_ID>` do README não teria como ser obtido. `HasData` torna o seed determinístico, versionado e aplicado automaticamente junto das demais migrations.

**GUIDs fixos.** Permitem referenciar os produtos no README e em exemplos `curl` sem precisar consultar o banco antes.

**Impacto nos testes.** Os testes de integração semeiam seus próprios produtos com GUIDs aleatórios; os produtos fixos do seed não colidem nem interferem nas asserções (que contam pedidos, não produtos).
