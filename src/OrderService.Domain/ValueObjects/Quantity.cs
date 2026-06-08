using OrderService.Domain.Exceptions;

namespace OrderService.Domain.ValueObjects;

public sealed record Quantity
{
    public int Value { get; }

    private Quantity(int value) => Value = value;

    public static Quantity Of(int value)
    {
        if (value <= 0)
            throw new DomainException("A quantidade deve ser maior que zero.");
        return new Quantity(value);
    }

    public static implicit operator int(Quantity q) => q.Value;

    public override string ToString() => Value.ToString();
}
