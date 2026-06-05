using OrderService.Domain.Exceptions;

namespace OrderService.Domain.Entities;

public sealed class Product
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public decimal UnitPrice { get; private set; }
    public int AvailableQuantity { get; private set; }

    private Product() { }

    public static Product Create(Guid id, string name, decimal unitPrice, int availableQuantity)
    {
        if (id == Guid.Empty)
            throw new DomainException("O id do produto é obrigatório.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("O nome do produto é obrigatório.");
        if (unitPrice <= 0)
            throw new DomainException("O preço unitário deve ser maior que zero.");
        if (availableQuantity < 0)
            throw new DomainException("O estoque disponível não pode ser negativo.");

        return new Product
        {
            Id = id,
            Name = name.Trim(),
            UnitPrice = unitPrice,
            AvailableQuantity = availableQuantity
        };
    }

    public bool HasSufficientStock(int quantity) => AvailableQuantity >= quantity;
}
