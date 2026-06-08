using FluentAssertions;
using Moq;
using OrderService.Application.Exceptions;
using OrderService.Application.Services;
using OrderService.Domain.Entities;
using OrderService.Domain.Repositories;

namespace OrderService.UnitTests.Application;

public sealed class CancelOrderServiceTests
{
    private readonly Mock<IOrderRepository> _orderRepo = new();
    private readonly Mock<IProductRepository> _productRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly CancelOrderService _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    public CancelOrderServiceTests()
    {
        _sut = new CancelOrderService(_orderRepo.Object, _productRepo.Object, _unitOfWork.Object);

        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, CancellationToken>(async (op, _) => await op());
    }

    private static Order BuildPlacedOrder()
    {
        var item = OrderItem.Create(ProductId, 10m, "BRL", 3);
        return Order.Place(CustomerId, "BRL", new[] { item });
    }

    [Fact]
    public async Task CancelAsync_PlacedOrder_CancelsWithoutReleasingStock()
    {
        var order = BuildPlacedOrder();
        _orderRepo.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);

        var response = await _sut.CancelAsync(order.Id, CustomerId);

        response.Status.Should().Be("Canceled");
        _productRepo.Verify(r => r.ReleaseStockAsync(It.IsAny<Guid>(), It.IsAny<int>(), default), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_ConfirmedOrder_CancelsAndReleasesStock()
    {
        var order = BuildPlacedOrder();
        order.Confirm();
        _orderRepo.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);

        var response = await _sut.CancelAsync(order.Id, CustomerId);

        response.Status.Should().Be("Canceled");
        _productRepo.Verify(r => r.ReleaseStockAsync(ProductId, 3, default), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_AlreadyCanceled_ReturnsIdempotentWithoutExtraOps()
    {
        var order = BuildPlacedOrder();
        order.Cancel();
        _orderRepo.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);

        var response = await _sut.CancelAsync(order.Id, CustomerId);

        response.Status.Should().Be("Canceled");
        _productRepo.Verify(r => r.ReleaseStockAsync(It.IsAny<Guid>(), It.IsAny<int>(), default), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task CancelAsync_OrderNotFound_ThrowsNotFoundException()
    {
        var missingId = Guid.NewGuid();
        _orderRepo.Setup(r => r.GetByIdAsync(missingId, default)).ReturnsAsync((Order?)null);

        await _sut.Invoking(s => s.CancelAsync(missingId, CustomerId))
            .Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{missingId}*");
    }

    [Fact]
    public async Task CancelAsync_WrongCustomer_ThrowsForbiddenException()
    {
        var order = BuildPlacedOrder();
        _orderRepo.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);

        await _sut.Invoking(s => s.CancelAsync(order.Id, Guid.NewGuid()))
            .Should().ThrowAsync<ForbiddenException>();
    }
}
