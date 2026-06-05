using FluentAssertions;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;
using OrderService.Domain.Exceptions;

namespace OrderService.UnitTests.Domain;

public sealed class OrderTests
{
    private static OrderItem ValidItem(decimal unitPrice = 10m, int quantity = 2) =>
        OrderItem.Create(Guid.NewGuid(), unitPrice, quantity);

    // ─── Criação (Place) ────────────────────────────────────────────────────

    [Fact]
    public void Place_ComListaDeItensVazia_DeveLancarDomainException()
    {
        var act = () => Order.Place(Guid.NewGuid(), "BRL", []);

        act.Should().Throw<DomainException>()
            .WithMessage("*item*");
    }

    [Fact]
    public void Place_ComItensNulos_DeveLancarDomainException()
    {
        var act = () => Order.Place(Guid.NewGuid(), "BRL", null!);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Place_ComCustomerIdVazio_DeveLancarDomainException()
    {
        var act = () => Order.Place(Guid.Empty, "BRL", [ValidItem()]);

        act.Should().Throw<DomainException>()
            .WithMessage("*cliente*");
    }

    [Fact]
    public void Place_ComCurrencyVazia_DeveLancarDomainException()
    {
        var act = () => Order.Place(Guid.NewGuid(), "   ", [ValidItem()]);

        act.Should().Throw<DomainException>()
            .WithMessage("*moeda*");
    }

    [Fact]
    public void Place_Valido_StatusDeveSer_Placed()
    {
        var order = Order.Place(Guid.NewGuid(), "BRL", [ValidItem()]);

        order.Status.Should().Be(OrderStatus.Placed);
    }

    [Fact]
    public void Place_Valido_TotalDeveSerSomaDosItens()
    {
        var items = new[]
        {
            OrderItem.Create(Guid.NewGuid(), 10m, 3),  // 30
            OrderItem.Create(Guid.NewGuid(), 25m, 2),  // 50
        };

        var order = Order.Place(Guid.NewGuid(), "BRL", items);

        order.Total.Should().Be(80m);
    }

    [Fact]
    public void Place_Valido_IdNaoDeveSerVazio()
    {
        var order = Order.Place(Guid.NewGuid(), "BRL", [ValidItem()]);

        order.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Place_Valido_CreatedAtDeveSerPreenchido()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);

        var order = Order.Place(Guid.NewGuid(), "BRL", [ValidItem()]);

        order.CreatedAt.Should().BeAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
    }

    // ─── Confirm ────────────────────────────────────────────────────────────

    [Fact]
    public void Confirm_DePlaced_DeveMudarStatusParaConfirmed()
    {
        var order = Order.Place(Guid.NewGuid(), "BRL", [ValidItem()]);

        order.Confirm();

        order.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public void Confirm_JaConfirmado_DeveSerIdempotente()
    {
        var order = Order.Place(Guid.NewGuid(), "BRL", [ValidItem()]);
        order.Confirm();

        var act = () => order.Confirm();     // segunda chamada

        act.Should().NotThrow();
        order.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public void Confirm_DeCanceled_DeveLancarDomainException()
    {
        var order = Order.Place(Guid.NewGuid(), "BRL", [ValidItem()]);
        order.Cancel();

        var act = () => order.Confirm();

        act.Should().Throw<DomainException>();
    }

    // ─── Cancel ─────────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_DePlaced_DeveMudarStatusParaCanceled()
    {
        var order = Order.Place(Guid.NewGuid(), "BRL", [ValidItem()]);

        order.Cancel();

        order.Status.Should().Be(OrderStatus.Canceled);
    }

    [Fact]
    public void Cancel_DeConfirmed_DeveMudarStatusParaCanceled()
    {
        var order = Order.Place(Guid.NewGuid(), "BRL", [ValidItem()]);
        order.Confirm();

        order.Cancel();

        order.Status.Should().Be(OrderStatus.Canceled);
    }

    [Fact]
    public void Cancel_JaCancelado_DeveSerIdempotente()
    {
        var order = Order.Place(Guid.NewGuid(), "BRL", [ValidItem()]);
        order.Cancel();

        var act = () => order.Cancel();     // segunda chamada

        act.Should().NotThrow();
        order.Status.Should().Be(OrderStatus.Canceled);
    }

    // ─── Total derivado (invariante) ─────────────────────────────────────────

    [Fact]
    public void Total_DeveReflitirTodosOsItens()
    {
        var items = new[]
        {
            OrderItem.Create(Guid.NewGuid(), 5m, 4),   // 20
            OrderItem.Create(Guid.NewGuid(), 3m, 10),  // 30
            OrderItem.Create(Guid.NewGuid(), 7m, 1),   //  7
        };

        var order = Order.Place(Guid.NewGuid(), "USD", items);

        order.Total.Should().Be(57m);
    }
}
