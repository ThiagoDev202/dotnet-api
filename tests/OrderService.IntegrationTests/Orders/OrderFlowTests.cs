using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using OrderService.Application.DTOs;
using OrderService.IntegrationTests.Infrastructure;

namespace OrderService.IntegrationTests.Orders;

[Collection("Integration")]
public sealed class OrderFlowTests
{
    private readonly OrderApiFactory _factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OrderFlowTests(OrderApiFactory factory) => _factory = factory;

    // ── Criação ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task POST_orders_com_dados_validos_retorna_201_e_status_Placed()
    {
        var productId = await _factory.SeedProductAsync("Livro", 49.90m, 5);
        var client = _factory.CreateAuthenticatedClient(Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/orders",
            new CreateOrderRequest("BRL", [new OrderItemRequest(productId, 2)]));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        order.Should().NotBeNull();
        order!.Status.Should().Be("Placed");
        order.Total.Should().Be(2 * 49.90m);
        order.Items.Should().HaveCount(1);
        order.Items[0].Quantity.Should().Be(2);
    }

    [Fact]
    public async Task POST_orders_sem_itens_retorna_400()
    {
        var client = _factory.CreateAuthenticatedClient(Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/orders",
            new CreateOrderRequest("BRL", []));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_orders_com_produto_inexistente_retorna_422()
    {
        var client = _factory.CreateAuthenticatedClient(Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/orders",
            new CreateOrderRequest("BRL", [new OrderItemRequest(Guid.NewGuid(), 1)]));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task POST_orders_excedendo_estoque_retorna_422()
    {
        var productId = await _factory.SeedProductAsync("Caneta", 5m, 3);
        var client = _factory.CreateAuthenticatedClient(Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/orders",
            new CreateOrderRequest("BRL", [new OrderItemRequest(productId, 10)]));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Confirm ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Confirm_decrementa_estoque_e_retorna_status_Confirmed()
    {
        var productId = await _factory.SeedProductAsync("Mouse", 120m, 5);
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId);

        var orderId = await CreateOrderAsync(client, productId, 2);

        var confirmResp = await client.PostAsync($"/orders/{orderId}/confirm", null);
        confirmResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var order = await confirmResp.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        order!.Status.Should().Be("Confirmed");

        var stock = await _factory.GetProductStockAsync(productId);
        stock.Should().Be(3); // 5 - 2
    }

    [Fact]
    public async Task Confirm_e_idempotente_segunda_chamada_retorna_200_sem_decrementar_novamente()
    {
        var productId = await _factory.SeedProductAsync("Teclado", 200m, 10);
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId);

        var orderId = await CreateOrderAsync(client, productId, 3);

        // Primeira confirmação
        (await client.PostAsync($"/orders/{orderId}/confirm", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var stockAfterFirst = await _factory.GetProductStockAsync(productId);

        // Segunda confirmação (idempotente)
        (await client.PostAsync($"/orders/{orderId}/confirm", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var stockAfterSecond = await _factory.GetProductStockAsync(productId);
        stockAfterSecond.Should().Be(stockAfterFirst); // estoque não alterado
    }

    // ── Cancel ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_de_Placed_retorna_200_e_status_Canceled_sem_alterar_estoque()
    {
        var productId = await _factory.SeedProductAsync("Cadeira", 800m, 4);
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId);

        var orderId = await CreateOrderAsync(client, productId, 2);
        var stockBefore = await _factory.GetProductStockAsync(productId);

        var cancelResp = await client.PostAsync($"/orders/{orderId}/cancel", null);
        cancelResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var order = await cancelResp.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        order!.Status.Should().Be("Canceled");

        // Pedido Placed não baixou estoque, então Cancel não deve devolver
        var stockAfter = await _factory.GetProductStockAsync(productId);
        stockAfter.Should().Be(stockBefore);
    }

    [Fact]
    public async Task Fluxo_create_confirm_cancel_devolve_estoque()
    {
        var productId = await _factory.SeedProductAsync("Monitor", 1500m, 6);
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId);

        var stockInitial = await _factory.GetProductStockAsync(productId);
        var orderId = await CreateOrderAsync(client, productId, 2);

        await client.PostAsync($"/orders/{orderId}/confirm", null);
        var stockAfterConfirm = await _factory.GetProductStockAsync(productId);
        stockAfterConfirm.Should().Be(stockInitial - 2);

        var cancelResp = await client.PostAsync($"/orders/{orderId}/cancel", null);
        cancelResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var stockAfterCancel = await _factory.GetProductStockAsync(productId);
        stockAfterCancel.Should().Be(stockInitial); // estoque restaurado
    }

    [Fact]
    public async Task Cancel_e_idempotente_segunda_chamada_retorna_200_sem_devolver_novamente()
    {
        var productId = await _factory.SeedProductAsync("Mesa", 600m, 5);
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId);

        var orderId = await CreateOrderAsync(client, productId, 1);

        await client.PostAsync($"/orders/{orderId}/confirm", null);
        await client.PostAsync($"/orders/{orderId}/cancel", null);
        var stockAfterFirst = await _factory.GetProductStockAsync(productId);

        // Segunda chamada
        (await client.PostAsync($"/orders/{orderId}/cancel", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var stockAfterSecond = await _factory.GetProductStockAsync(productId);
        stockAfterSecond.Should().Be(stockAfterFirst); // não devolveu de novo
    }

    // ── GET e listagem ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_order_retorna_200_com_dados_corretos()
    {
        var productId = await _factory.SeedProductAsync("Fone", 300m, 10);
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId);

        var orderId = await CreateOrderAsync(client, productId, 3);

        var response = await client.GetAsync($"/orders/{orderId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        order.Should().NotBeNull();
        order!.Id.Should().Be(orderId);
        order.CustomerId.Should().Be(customerId);
        order.Items.Should().HaveCount(1);
        order.Total.Should().Be(3 * 300m);
    }

    [Fact]
    public async Task GET_orders_retorna_lista_paginada()
    {
        var productId = await _factory.SeedProductAsync("Copo", 20m, 50);
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId);

        // Cria 3 pedidos
        for (var i = 0; i < 3; i++)
            await CreateOrderAsync(client, productId, 1);

        var response = await client.GetAsync("/orders?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content
            .ReadFromJsonAsync<PagedResult<OrderResponse>>(JsonOptions);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCountGreaterThanOrEqualTo(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GET_orders_filtra_por_status()
    {
        var productId = await _factory.SeedProductAsync("Caixa", 10m, 20);
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId);

        var order1 = await CreateOrderAsync(client, productId, 1);
        var order2 = await CreateOrderAsync(client, productId, 1);

        // Confirma apenas o primeiro
        await client.PostAsync($"/orders/{order1}/confirm", null);

        var response = await client.GetAsync("/orders?status=Confirmed&page=1&pageSize=20");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content
            .ReadFromJsonAsync<PagedResult<OrderResponse>>(JsonOptions);
        result!.Items.Should().OnlyContain(o => o.Status == "Confirmed");
        result.Items.Should().Contain(o => o.Id == order1);
        result.Items.Should().NotContain(o => o.Id == order2);
    }

    [Fact]
    public async Task GET_orders_paginacao_segunda_pagina_retorna_menos_resultados()
    {
        var productId = await _factory.SeedProductAsync("Papel", 2m, 100);
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId);

        // Cria 5 pedidos
        for (var i = 0; i < 5; i++)
            await CreateOrderAsync(client, productId, 1);

        var page1 = await (await client.GetAsync("/orders?page=1&pageSize=3"))
            .Content.ReadFromJsonAsync<PagedResult<OrderResponse>>(JsonOptions);
        var page2 = await (await client.GetAsync("/orders?page=2&pageSize=3"))
            .Content.ReadFromJsonAsync<PagedResult<OrderResponse>>(JsonOptions);

        page1!.Items.Should().HaveCount(3);
        page2!.Items.Count.Should().BeGreaterThan(0);
        page1.Items.Select(o => o.Id).Should()
            .NotIntersectWith(page2.Items.Select(o => o.Id));
    }

    // ── Helper ─────────────────────────────────────────────────────────────────

    private static async Task<Guid> CreateOrderAsync(
        HttpClient client, Guid productId, int quantity)
    {
        var resp = await client.PostAsJsonAsync("/orders",
            new CreateOrderRequest("BRL", [new OrderItemRequest(productId, quantity)]));
        resp.EnsureSuccessStatusCode();
        var order = await resp.Content
            .ReadFromJsonAsync<OrderResponse>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        return order!.Id;
    }
}
