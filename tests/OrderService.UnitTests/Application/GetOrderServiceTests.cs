using FluentAssertions;
using Moq;
using OrderService.Application.Exceptions;
using OrderService.Application.Services;
using OrderService.Domain.Entities;
using OrderService.Domain.Repositories;

namespace OrderService.UnitTests.Application;

public sealed class GetOrderServiceTests
{
    private readonly Mock<IOrderRepository> _orderRepo = new();
    private readonly GetOrderService _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    public GetOrderServiceTests()
    {
        _sut = new GetOrderService(_orderRepo.Object);
    }

    private static Order BuildOrder(Guid customerId)
    {
        var item = OrderItem.Create(ProductId, 10m, 1);
        return Order.Place(customerId, "BRL", new[] { item });
    }

    [Fact]
    public async Task GetByIdAsync_OrderExists_ReturnsResponse()
    {
        var order = BuildOrder(CustomerId);
        _orderRepo.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);

        var response = await _sut.GetByIdAsync(order.Id, CustomerId, isAdmin: false);

        response.Id.Should().Be(order.Id);
        response.CustomerId.Should().Be(CustomerId);
        response.Status.Should().Be("Placed");
    }

    [Fact]
    public async Task GetByIdAsync_OrderNotFound_ThrowsNotFoundException()
    {
        var missingId = Guid.NewGuid();
        _orderRepo.Setup(r => r.GetByIdAsync(missingId, default)).ReturnsAsync((Order?)null);

        await _sut.Invoking(s => s.GetByIdAsync(missingId, CustomerId, isAdmin: false))
            .Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{missingId}*");
    }

    [Fact]
    public async Task GetByIdAsync_WrongCustomerNonAdmin_ThrowsForbiddenException()
    {
        var order = BuildOrder(CustomerId);
        _orderRepo.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);

        await _sut.Invoking(s => s.GetByIdAsync(order.Id, Guid.NewGuid(), isAdmin: false))
            .Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GetByIdAsync_AdminCanAccessAnyOrder()
    {
        var order = BuildOrder(CustomerId);
        _orderRepo.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);

        var response = await _sut.GetByIdAsync(order.Id, Guid.NewGuid(), isAdmin: true);

        response.Id.Should().Be(order.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ResponseMapsItemsCorrectly()
    {
        var order = BuildOrder(CustomerId);
        _orderRepo.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);

        var response = await _sut.GetByIdAsync(order.Id, CustomerId, isAdmin: false);

        response.Items.Should().HaveCount(1);
        response.Items[0].ProductId.Should().Be(ProductId);
        response.Items[0].UnitPrice.Should().Be(10m);
        response.Items[0].Quantity.Should().Be(1);
        response.Items[0].Subtotal.Should().Be(10m);
    }
}
