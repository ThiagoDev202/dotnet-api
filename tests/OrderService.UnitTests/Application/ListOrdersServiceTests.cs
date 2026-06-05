using FluentAssertions;
using Moq;
using OrderService.Application.DTOs;
using OrderService.Application.Repositories;
using OrderService.Application.Services;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;

namespace OrderService.UnitTests.Application;

public sealed class ListOrdersServiceTests
{
    private readonly Mock<IOrderQueryRepository> _queryRepo = new();
    private readonly ListOrdersService _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid OtherCustomerId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    public ListOrdersServiceTests()
    {
        _sut = new ListOrdersService(_queryRepo.Object);
    }

    private static Order BuildOrder(Guid customerId)
    {
        var item = OrderItem.Create(ProductId, 5m, 2);
        return Order.Place(customerId, "BRL", new[] { item });
    }

    private static PagedResult<Order> EmptyPage(int page = 1, int pageSize = 20) =>
        new([], 0, page, pageSize);

    [Fact]
    public async Task ListAsync_NonAdmin_AlwaysOverridesFilterWithOwnCustomerId()
    {
        OrderFilter? capturedFilter = null;
        _queryRepo
            .Setup(r => r.ListAsync(It.IsAny<OrderFilter>(), default))
            .Callback<OrderFilter, CancellationToken>((f, _) => capturedFilter = f)
            .ReturnsAsync(EmptyPage());

        var requestedFilter = new OrderFilter(CustomerId: OtherCustomerId);
        await _sut.ListAsync(requestedFilter, CustomerId, isAdmin: false);

        capturedFilter!.CustomerId.Should().Be(CustomerId);
    }

    [Fact]
    public async Task ListAsync_Admin_PassesFilterAsIs()
    {
        OrderFilter? capturedFilter = null;
        _queryRepo
            .Setup(r => r.ListAsync(It.IsAny<OrderFilter>(), default))
            .Callback<OrderFilter, CancellationToken>((f, _) => capturedFilter = f)
            .ReturnsAsync(EmptyPage());

        var requestedFilter = new OrderFilter(CustomerId: OtherCustomerId);
        await _sut.ListAsync(requestedFilter, CustomerId, isAdmin: true);

        capturedFilter!.CustomerId.Should().Be(OtherCustomerId);
    }

    [Fact]
    public async Task ListAsync_MapsPagedResultCorrectly()
    {
        var order = BuildOrder(CustomerId);
        var pagedOrders = new PagedResult<Order>(new[] { order }, 1, 1, 20);
        _queryRepo
            .Setup(r => r.ListAsync(It.IsAny<OrderFilter>(), default))
            .ReturnsAsync(pagedOrders);

        var result = await _sut.ListAsync(new OrderFilter(), CustomerId, isAdmin: false);

        result.TotalCount.Should().Be(1);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.Items.Should().HaveCount(1);
        result.Items[0].CustomerId.Should().Be(CustomerId);
    }

    [Fact]
    public async Task ListAsync_TotalPages_IsCalculatedCorrectly()
    {
        var orders = Enumerable.Range(0, 5).Select(_ => BuildOrder(CustomerId)).ToList();
        var pagedOrders = new PagedResult<Order>(orders, TotalCount: 25, Page: 1, PageSize: 5);
        _queryRepo
            .Setup(r => r.ListAsync(It.IsAny<OrderFilter>(), default))
            .ReturnsAsync(pagedOrders);

        var result = await _sut.ListAsync(new OrderFilter(), CustomerId, isAdmin: false);

        result.TotalPages.Should().Be(5);
        result.HasNextPage.Should().BeTrue();
        result.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public async Task ListAsync_PassesStatusAndDateFiltersToRepository()
    {
        OrderFilter? capturedFilter = null;
        _queryRepo
            .Setup(r => r.ListAsync(It.IsAny<OrderFilter>(), default))
            .Callback<OrderFilter, CancellationToken>((f, _) => capturedFilter = f)
            .ReturnsAsync(EmptyPage());

        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;
        var requestedFilter = new OrderFilter(Status: OrderStatus.Placed, From: from, To: to);

        await _sut.ListAsync(requestedFilter, CustomerId, isAdmin: true);

        capturedFilter!.Status.Should().Be(OrderStatus.Placed);
        capturedFilter.From.Should().Be(from);
        capturedFilter.To.Should().Be(to);
    }
}
