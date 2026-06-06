using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using OrderService.Application.DTOs;
using OrderService.IntegrationTests.Infrastructure;

namespace OrderService.IntegrationTests.Isolation;

/// <summary>
/// Garante o isolamento de pedidos entre clientes com o RLS ATIVO (runtime conecta como
/// orders_app, igual à produção). Sob RLS, o pedido de outro cliente é invisível: acesso
/// cross-tenant retorna 404 (não 403) — o banco filtra a linha antes da camada de aplicação,
/// o que também evita vazar a existência do recurso.
/// </summary>
[Collection("RlsIntegration")]
public sealed class AccessControlTests
{
    private readonly RlsApiFactory _factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AccessControlTests(RlsApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GET_order_de_outro_cliente_retorna_404_pois_RLS_oculta_a_linha()
    {
        var productId = await _factory.SeedProductAsync("Produto RLS-1", 30m, 5);
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();

        var clientA = _factory.CreateAuthenticatedClient(customerA);
        var clientB = _factory.CreateAuthenticatedClient(customerB);

        var orderIdA = await CreateOrderAsync(clientA, productId, 1);

        var response = await clientB.GetAsync($"/orders/{orderIdA}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Confirm_de_pedido_de_outro_cliente_retorna_404_pois_RLS_oculta_a_linha()
    {
        var productId = await _factory.SeedProductAsync("Produto RLS-2", 80m, 5);
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();

        var clientA = _factory.CreateAuthenticatedClient(customerA);
        var clientB = _factory.CreateAuthenticatedClient(customerB);

        var orderIdA = await CreateOrderAsync(clientA, productId, 1);

        var response = await clientB.PostAsync($"/orders/{orderIdA}/confirm", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cancel_de_pedido_de_outro_cliente_retorna_404_pois_RLS_oculta_a_linha()
    {
        var productId = await _factory.SeedProductAsync("Produto RLS-3", 60m, 5);
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();

        var clientA = _factory.CreateAuthenticatedClient(customerA);
        var clientB = _factory.CreateAuthenticatedClient(customerB);

        var orderIdA = await CreateOrderAsync(clientA, productId, 1);

        var response = await clientB.PostAsync($"/orders/{orderIdA}/cancel", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_orders_retorna_apenas_pedidos_do_cliente_autenticado()
    {
        var productId = await _factory.SeedProductAsync("Produto RLS-4", 10m, 20);
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();

        var clientA = _factory.CreateAuthenticatedClient(customerA);
        var clientB = _factory.CreateAuthenticatedClient(customerB);

        // A cria 2 pedidos, B cria 1
        var orderA1 = await CreateOrderAsync(clientA, productId, 1);
        var orderA2 = await CreateOrderAsync(clientA, productId, 1);
        var orderB1 = await CreateOrderAsync(clientB, productId, 1);

        // A só vê os seus
        var resultA = await (await clientA.GetAsync("/orders?page=1&pageSize=50"))
            .Content.ReadFromJsonAsync<PagedResult<OrderResponse>>(JsonOptions);

        resultA!.Items.Should().Contain(o => o.Id == orderA1);
        resultA.Items.Should().Contain(o => o.Id == orderA2);
        resultA.Items.Should().NotContain(o => o.Id == orderB1);

        // B só vê o seu
        var resultB = await (await clientB.GetAsync("/orders?page=1&pageSize=50"))
            .Content.ReadFromJsonAsync<PagedResult<OrderResponse>>(JsonOptions);

        resultB!.Items.Should().Contain(o => o.Id == orderB1);
        resultB.Items.Should().NotContain(o => o.Id == orderA1);
        resultB.Items.Should().NotContain(o => o.Id == orderA2);
    }

    [Fact]
    public async Task Admin_pode_listar_pedidos_de_qualquer_cliente()
    {
        var productId = await _factory.SeedProductAsync("Produto RLS-5", 25m, 20);
        var customerA = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var clientA = _factory.CreateAuthenticatedClient(customerA);
        var adminClient = _factory.CreateAuthenticatedClient(adminId, role: "Admin");

        var orderIdA = await CreateOrderAsync(clientA, productId, 1);

        // Admin consulta diretamente pelo ID (sem restrição de customerId)
        var response = await adminClient.GetAsync($"/orders/{orderIdA}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        order!.CustomerId.Should().Be(customerA);
    }

    [Fact]
    public async Task Admin_pode_listar_pedidos_filtrado_por_customerId_externo()
    {
        var productId = await _factory.SeedProductAsync("Produto RLS-6", 15m, 20);
        var customerA = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var clientA = _factory.CreateAuthenticatedClient(customerA);
        var adminClient = _factory.CreateAuthenticatedClient(adminId, role: "Admin");

        var orderIdA = await CreateOrderAsync(clientA, productId, 1);

        var result = await (await adminClient.GetAsync($"/orders?customerId={customerA}&page=1&pageSize=50"))
            .Content.ReadFromJsonAsync<PagedResult<OrderResponse>>(JsonOptions);

        result!.Items.Should().Contain(o => o.Id == orderIdA);
        result.Items.Should().OnlyContain(o => o.CustomerId == customerA);
    }

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
