using OrderService.Domain.Exceptions;
using OrderService.Domain.ValueObjects;

namespace OrderService.Domain.Entities;

public sealed class OrderItem
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public Money UnitPrice { get; private set; } = null!;
    public Quantity Quantity { get; private set; } = null!;

    private OrderItem() { }

    public static OrderItem Create(Guid productId, decimal amount, string currency, int quantity)
    {
        if (productId == Guid.Empty)
            throw new DomainException("O id do produto é obrigatório.");
        if (amount <= 0)
            throw new DomainException("O preço unitário deve ser maior que zero.");

        return new OrderItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            UnitPrice = Money.Of(amount, currency),
            Quantity = Quantity.Of(quantity)
        };
    }
}
