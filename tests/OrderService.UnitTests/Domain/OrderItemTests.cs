using FluentAssertions;
using OrderService.Domain.Entities;
using OrderService.Domain.Exceptions;

namespace OrderService.UnitTests.Domain;

public sealed class OrderItemTests
{
    private readonly Guid _validProductId = Guid.NewGuid();

    // ─── Quantity boundary ───────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Create_ComQuantidadeInvalida_DeveLancarDomainException(int quantity)
    {
        var act = () => OrderItem.Create(_validProductId, 10m, quantity);

        act.Should().Throw<DomainException>()
            .WithMessage("*quantidade*");
    }

    [Fact]
    public void Create_ComQuantidadeUm_DeveSucceder()
    {
        var item = OrderItem.Create(_validProductId, 10m, 1);

        item.Quantity.Should().Be(1);
    }

    // ─── UnitPrice boundary ─────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void Create_ComUnitPriceInvalido_DeveLancarDomainException(decimal unitPrice)
    {
        var act = () => OrderItem.Create(_validProductId, unitPrice, 1);

        act.Should().Throw<DomainException>()
            .WithMessage("*preço*");
    }

    [Fact]
    public void Create_ComUnitPriceMinimo_DeveSucceder()
    {
        var item = OrderItem.Create(_validProductId, 0.01m, 1);

        item.UnitPrice.Should().Be(0.01m);
    }

    // ─── ProductId ──────────────────────────────────────────────────────────

    [Fact]
    public void Create_ComProductIdVazio_DeveLancarDomainException()
    {
        var act = () => OrderItem.Create(Guid.Empty, 10m, 1);

        act.Should().Throw<DomainException>()
            .WithMessage("*produto*");
    }

    // ─── Propriedades ────────────────────────────────────────────────────────

    [Fact]
    public void Create_Valido_DevePersistirPropriedades()
    {
        var productId = Guid.NewGuid();

        var item = OrderItem.Create(productId, 12.50m, 4);

        item.ProductId.Should().Be(productId);
        item.UnitPrice.Should().Be(12.50m);
        item.Quantity.Should().Be(4);
        item.Id.Should().NotBe(Guid.Empty);
    }
}
