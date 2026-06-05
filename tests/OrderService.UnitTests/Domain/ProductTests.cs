using FluentAssertions;
using OrderService.Domain.Entities;
using OrderService.Domain.Exceptions;

namespace OrderService.UnitTests.Domain;

public sealed class ProductTests
{
    private static Product CreateProduct(int availableQuantity = 10) =>
        Product.Create(Guid.NewGuid(), "Produto Teste", 50m, availableQuantity);

    // ─── HasSufficientStock ──────────────────────────────────────────────────

    [Fact]
    public void HasSufficientStock_ComEstoqueMaiorQueQtd_DeveRetornarTrue()
    {
        var product = CreateProduct(availableQuantity: 10);

        product.HasSufficientStock(5).Should().BeTrue();
    }

    [Fact]
    public void HasSufficientStock_ComEstoqueExatamenteIgual_DeveRetornarTrue()
    {
        var product = CreateProduct(availableQuantity: 5);

        product.HasSufficientStock(5).Should().BeTrue();
    }

    [Fact]
    public void HasSufficientStock_ComEstoqueMenorQueQtd_DeveRetornarFalse()
    {
        var product = CreateProduct(availableQuantity: 3);

        product.HasSufficientStock(5).Should().BeFalse();
    }

    [Fact]
    public void HasSufficientStock_ComEstoqueZero_DeveRetornarFalse()
    {
        var product = CreateProduct(availableQuantity: 0);

        product.HasSufficientStock(1).Should().BeFalse();
    }

    // ─── Criação com valores inválidos ───────────────────────────────────────

    [Fact]
    public void Create_ComNomeVazio_DeveLancarDomainException()
    {
        var act = () => Product.Create(Guid.NewGuid(), "  ", 10m, 5);

        act.Should().Throw<DomainException>()
            .WithMessage("*nome*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_ComUnitPriceInvalido_DeveLancarDomainException(decimal price)
    {
        var act = () => Product.Create(Guid.NewGuid(), "Produto", price, 5);

        act.Should().Throw<DomainException>()
            .WithMessage("*preço*");
    }

    [Fact]
    public void Create_ComQuantidadeNegativa_DeveLancarDomainException()
    {
        var act = () => Product.Create(Guid.NewGuid(), "Produto", 10m, -1);

        act.Should().Throw<DomainException>()
            .WithMessage("*estoque*");
    }
}
