using OrderService.Domain.Enums;
using OrderService.Domain.Exceptions;
using OrderService.Domain.ValueObjects;

namespace OrderService.Domain.Entities;

public sealed class Order
{
    private readonly List<OrderItem> _items = [];

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public string Currency { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public Money Total
    {
        get
        {
            var amount = _items.Sum(i => i.UnitPrice * i.Quantity.Value);
            return Money.Of(amount, Currency);
        }
    }

    private Order() { }

    public static Order Place(Guid customerId, string currency, IEnumerable<OrderItem> items)
    {
        if (customerId == Guid.Empty)
            throw new DomainException("O id do cliente é obrigatório.");
        if (string.IsNullOrWhiteSpace(currency))
            throw new DomainException("A moeda é obrigatória.");

        var itemList = items?.ToList()
            ?? throw new DomainException("A lista de itens não pode ser nula.");

        if (itemList.Count == 0)
            throw new DomainException("O pedido deve ter ao menos um item.");

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Currency = currency.Trim().ToUpperInvariant(),
            Status = OrderStatus.Placed,
            CreatedAt = DateTime.UtcNow
        };
        order._items.AddRange(itemList);

        return order;
    }

    public void Confirm()
    {
        if (Status == OrderStatus.Confirmed)
            return;

        if (Status != OrderStatus.Placed)
            throw new DomainException(
                $"Não é possível confirmar um pedido com status '{Status}'.");

        Status = OrderStatus.Confirmed;
    }

    public void Cancel()
    {
        if (Status == OrderStatus.Canceled)
            return;

        if (Status is not (OrderStatus.Placed or OrderStatus.Confirmed))
            throw new DomainException(
                $"Não é possível cancelar um pedido com status '{Status}'.");

        Status = OrderStatus.Canceled;
    }
}
