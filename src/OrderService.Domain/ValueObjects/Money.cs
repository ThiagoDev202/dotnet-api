using OrderService.Domain.Exceptions;

namespace OrderService.Domain.ValueObjects;

public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Of(decimal amount, string currency)
    {
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

    public override string ToString() => $"{Amount:F2} {Currency}";
}
