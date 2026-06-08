using OrderService.Domain.Exceptions;

namespace OrderService.Domain.ValueObjects;

public sealed class Money : IEquatable<Money>
{
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;

    private Money() { }  // for EF Core OwnsOne

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Of(decimal amount, string currency)
    {
        // Permite 0 para suportar o valor identidade em somas (ex.: Total de um agregado vazio);
        // OrderItem.Create valida amount > 0 separadamente.
        if (amount < 0)
            throw new DomainException("O valor não pode ser negativo.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new DomainException("A moeda deve ser um código ISO-4217 de 3 caracteres.");
        return new Money(amount, currency.Trim().ToUpperInvariant());
    }

    public static Money operator +(Money a, Money b)
    {
        if (!string.Equals(a.Currency, b.Currency, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Não é possível somar valores em moedas diferentes.");
        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public bool Equals(Money? other) =>
        other is not null &&
        Amount == other.Amount &&
        string.Equals(Currency, other.Currency, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as Money);

    public override int GetHashCode() => HashCode.Combine(Amount, Currency.ToUpperInvariant());

    public static bool operator ==(Money? a, Money? b) => a is null ? b is null : a.Equals(b);

    public static bool operator !=(Money? a, Money? b) => !(a == b);

    public override string ToString() => $"{Amount:F2} {Currency}";
}
