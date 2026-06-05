using OrderService.Domain.Exceptions;

namespace OrderService.Domain.Entities;

public sealed class OrderItem
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }

    private OrderItem() { }

    public static OrderItem Create(Guid productId, decimal unitPrice, int quantity)
    {
        if (productId == Guid.Empty)
            throw new DomainException("O id do produto é obrigatório.");
        if (unitPrice <= 0)
            throw new DomainException("O preço unitário deve ser maior que zero.");
        if (quantity <= 0)
            throw new DomainException("A quantidade deve ser maior que zero.");

        return new OrderItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            UnitPrice = unitPrice,
            Quantity = quantity
        };
    }
}
