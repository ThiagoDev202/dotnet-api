using FluentAssertions;
using Moq;
using OrderService.Application.DTOs;
using OrderService.Application.Exceptions;
using OrderService.Application.Services;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;
using OrderService.Domain.Exceptions;
using OrderService.Domain.Repositories;

namespace OrderService.UnitTests.Application;

public sealed class ConfirmOrderServiceTests
{
    private readonly Mock<IOrderRepository> _orderRepo = new();
    private readonly Mock<IProductRepository> _productRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ConfirmOrderService _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    public ConfirmOrderServiceTests()
    {
        _sut = new ConfirmOrderService(_orderRepo.Object, _productRepo.Object, _unitOfWork.Object);

        // Simula ExecuteInTransactionAsync executando a operação diretamente
        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, CancellationToken>(async (op, _) => await op());
    }

    private static Order BuildPlacedOrder()
    {
        var item = OrderItem.Create(ProductId, 10m, 2);
        return Order.Place(CustomerId, "BRL", new[] { item });
    }

    [Fact]
    public async Task ConfirmAsync_PlacedOrder_ConfirmsAndDecrementsStock()
    {
        var order = BuildPlacedOrder();
        _orderRepo.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);
        _productRepo.Setup(r => r.TryDecrementStockAsync(ProductId, 2, default)).ReturnsAsync(true);

        var response = await _sut.ConfirmAsync(order.Id, CustomerId);

        response.Status.Should().Be("Confirmed");
        _productRepo.Verify(r => r.TryDecrementStockAsync(ProductId, 2, default), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task ConfirmAsync_AlreadyConfirmed_ReturnsIdempotentWithoutExtraOps()
    {
        var order = BuildPlacedOrder();
        order.Confirm();
        _orderRepo.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);

        var response = await _sut.ConfirmAsync(order.Id, CustomerId);

        response.Status.Should().Be("Confirmed");
        _productRepo.Verify(r => r.TryDecrementStockAsync(It.IsAny<Guid>(), It.IsAny<int>(), default), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task ConfirmAsync_OrderNotFound_ThrowsNotFoundException()
    {
        var missingId = Guid.NewGuid();
        _orderRepo.Setup(r => r.GetByIdAsync(missingId, default)).ReturnsAsync((Order?)null);

        await _sut.Invoking(s => s.ConfirmAsync(missingId, CustomerId))
            .Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{missingId}*");
    }

    [Fact]
    public async Task ConfirmAsync_WrongCustomer_ThrowsForbiddenException()
    {
        var order = BuildPlacedOrder();
        _orderRepo.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);

        await _sut.Invoking(s => s.ConfirmAsync(order.Id, Guid.NewGuid()))
            .Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ConfirmAsync_InsufficientStock_ThrowsDomainException()
    {
        var order = BuildPlacedOrder();
        _orderRepo.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);
        _productRepo.Setup(r => r.TryDecrementStockAsync(ProductId, 2, default)).ReturnsAsync(false);

        await _sut.Invoking(s => s.ConfirmAsync(order.Id, CustomerId))
            .Should().ThrowAsync<DomainException>()
            .WithMessage("*insuficiente*");
    }

    [Fact]
    public async Task ConfirmAsync_CanceledOrder_ThrowsDomainException()
    {
        var order = BuildPlacedOrder();
        order.Cancel();
        _orderRepo.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);

        await _sut.Invoking(s => s.ConfirmAsync(order.Id, CustomerId))
            .Should().ThrowAsync<DomainException>()
            .WithMessage("*confirmar*");
    }
}
