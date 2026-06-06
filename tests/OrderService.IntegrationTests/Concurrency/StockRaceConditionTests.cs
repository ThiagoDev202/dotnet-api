using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using OrderService.Application.DTOs;
using OrderService.IntegrationTests.Infrastructure;

namespace OrderService.IntegrationTests.Concurrency;

/// <summary>
/// Garante que o decremento atômico de estoque nunca permite overselling,
/// mesmo sob concorrência extrema de confirmações simultâneas.
///
/// Invariante verificada:
///   - Exatamente N confirmações têm sucesso (N = estoque disponível)
///   - Estoque final == 0 (nunca negativo)
///   - M - N confirmações falham com 422 (estoque insuficiente)
/// </summary>
[Collection("Integration")]
public sealed class StockRaceConditionTests
{
    private readonly OrderApiFactory _factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public StockRaceConditionTests(OrderApiFactory factory) => _factory = factory;

    [Fact]
    public async Task N_estoque_M_confirmacoes_concorrentes_nunca_gera_overselling()
    {
        const int N = 5;   // estoque disponível
        const int M = 10;  // confirmações concorrentes (M > N)

        var productId = await _factory.SeedProductAsync("Produto Race", 99m, N);
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId);

        // Pré-cria M pedidos sequencialmente (todos em status Placed)
        var orderIds = new Guid[M];
        for (var i = 0; i < M; i++)
        {
            var resp = await client.PostAsJsonAsync("/orders",
                new CreateOrderRequest("BRL", [new OrderItemRequest(productId, 1)]));
            resp.StatusCode.Should().Be(HttpStatusCode.Created,
                because: $"a criação do pedido {i + 1} deve ter sucesso");
            var order = await resp.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
            orderIds[i] = order!.Id;
        }

        // Confirma todos M pedidos ao mesmo tempo
        var confirmTasks = orderIds
            .Select(id => client.PostAsync($"/orders/{id}/confirm", null))
            .ToArray();

        var responses = await Task.WhenAll(confirmTasks);

        // Classifica os resultados
        var successes = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var failures = responses.Count(r =>
            r.StatusCode == HttpStatusCode.UnprocessableEntity ||
            r.StatusCode == HttpStatusCode.Conflict);

        // Exatamente N confirmações devem ter sucesso
        successes.Should().Be(N,
            because: $"com {N} unidades em estoque, apenas {N} de {M} confirmações simultâneas devem ter sucesso");

        // Todos os M devem ter tido resultado (sem erros inesperados)
        (successes + failures).Should().Be(M,
            because: "toda confirmação deve resultar em 200 (sucesso) ou 422 (estoque insuficiente)");

        // Estoque final deve ser exatamente 0 — nunca negativo
        var finalStock = await _factory.GetProductStockAsync(productId);
        finalStock.Should().Be(0,
            because: $"as {N} unidades em estoque devem ter sido consumidas exatamente, sem ir abaixo de zero");
    }

    [Fact]
    public async Task Estoque_nunca_fica_negativo_com_confirmacoes_em_lotes()
    {
        const int N = 3;
        const int M = 9;

        var productId = await _factory.SeedProductAsync("Produto Race 2", 50m, N);
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId);

        var orderIds = new Guid[M];
        for (var i = 0; i < M; i++)
        {
            var resp = await client.PostAsJsonAsync("/orders",
                new CreateOrderRequest("BRL", [new OrderItemRequest(productId, 1)]));
            var order = await resp.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
            orderIds[i] = order!.Id;
        }

        // Dispara em 3 lotes de 3 pedidos cada
        for (var batch = 0; batch < 3; batch++)
        {
            var batchIds = orderIds.Skip(batch * 3).Take(3).ToArray();
            var tasks = batchIds
                .Select(id => client.PostAsync($"/orders/{id}/confirm", null))
                .ToArray();
            await Task.WhenAll(tasks);
        }

        var finalStock = await _factory.GetProductStockAsync(productId);
        finalStock.Should().BeGreaterThanOrEqualTo(0,
            because: "o estoque nunca pode ser negativo, independentemente de quantas confirmações ocorram");
        finalStock.Should().Be(0,
            because: "as 3 unidades disponíveis devem ter sido consumidas sem ultrapassar o limite");
    }
}
