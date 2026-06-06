using FluentAssertions;
using Moq;
using OrderService.Application.DTOs;
using OrderService.Application.Services;
using OrderService.Domain.Entities;
using OrderService.Domain.Exceptions;
using OrderService.Domain.Repositories;

namespace OrderService.UnitTests.Application;

public sealed class CreateOrderServiceTests
{
    private readonly Mock<IProductRepository> _productRepo = new();
    private readonly Mock<IOrderRepository> _orderRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly CreateOrderService _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ProductId1 = Guid.NewGuid();
    private static readonly Guid ProductId2 = Guid.NewGuid();

    public CreateOrderServiceTests()
    {
        _sut = new CreateOrderService(_productRepo.Object, _orderRepo.Object, _unitOfWork.Object);
    }

    [Fact]
    public async Task CreateAsync_ValidSingleItem_ReturnsOrderResponseWithPlacedStatus()
    {
        var product = Product.Create(ProductId1, "Produto A", 50m, 10);
        _productRepo
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default))
            .ReturnsAsync(new List<Product> { product });

        var request = new CreateOrderRequest("BRL", new[] { new OrderItemRequest(ProductId1, 2) });

        var response = await _sut.CreateAsync(request, CustomerId);

        response.Status.Should().Be("Placed");
        response.CustomerId.Should().Be(CustomerId);
        response.Currency.Should().Be("BRL");
        response.Items.Should().HaveCount(1);
        response.Total.Should().Be(100m);
    }

    [Fact]
    public async Task CreateAsync_MultipleItems_TotalIsCorrectSum()
    {
        var p1 = Product.Create(ProductId1, "A", 10m, 5);
        var p2 = Product.Create(ProductId2, "B", 20m, 5);
        _productRepo
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default))
            .ReturnsAsync(new List<Product> { p1, p2 });

        var request = new CreateOrderRequest("USD", new[]
        {
            new OrderItemRequest(ProductId1, 3),
            new OrderItemRequest(ProductId2, 2)
        });

        var response = await _sut.CreateAsync(request, CustomerId);

        response.Total.Should().Be(70m); // (10*3) + (20*2)
        response.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateAsync_ProductNotFound_ThrowsDomainException()
    {
        _productRepo
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default))
            .ReturnsAsync(new List<Product>());

        var request = new CreateOrderRequest("BRL", new[] { new OrderItemRequest(ProductId1, 1) });

        await _sut.Invoking(s => s.CreateAsync(request, CustomerId))
            .Should().ThrowAsync<DomainException>()
            .WithMessage($"*{ProductId1}*");
    }

    [Fact]
    public async Task CreateAsync_InsufficientStock_ThrowsDomainException()
    {
        var product = Product.Create(ProductId1, "P", 10m, 2);
        _productRepo
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default))
            .ReturnsAsync(new List<Product> { product });

        var request = new CreateOrderRequest("BRL", new[] { new OrderItemRequest(ProductId1, 5) });

        await _sut.Invoking(s => s.CreateAsync(request, CustomerId))
            .Should().ThrowAsync<DomainException>()
            .WithMessage("*insuficiente*");
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_PersistsOrderAndSavesChanges()
    {
        var product = Product.Create(ProductId1, "P", 10m, 5);
        _productRepo
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default))
            .ReturnsAsync(new List<Product> { product });

        var request = new CreateOrderRequest("BRL", new[] { new OrderItemRequest(ProductId1, 1) });

        await _sut.CreateAsync(request, CustomerId);

        _orderRepo.Verify(r => r.AddAsync(It.IsAny<Order>(), default), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_UsesPriceFromProduct_NotFromRequest()
    {
        var product = Product.Create(ProductId1, "P", 99m, 5);
        _productRepo
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default))
            .ReturnsAsync(new List<Product> { product });

        var request = new CreateOrderRequest("BRL", new[] { new OrderItemRequest(ProductId1, 1) });

        var response = await _sut.CreateAsync(request, CustomerId);

        response.Items[0].UnitPrice.Should().Be(99m);
    }

    [Fact]
    public async Task CreateAsync_ResponseItemSubtotal_IsUnitPriceTimesQuantity()
    {
        var product = Product.Create(ProductId1, "P", 15m, 10);
        _productRepo
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default))
            .ReturnsAsync(new List<Product> { product });

        var request = new CreateOrderRequest("BRL", new[] { new OrderItemRequest(ProductId1, 4) });

        var response = await _sut.CreateAsync(request, CustomerId);

        response.Items[0].Subtotal.Should().Be(60m);
    }
}
