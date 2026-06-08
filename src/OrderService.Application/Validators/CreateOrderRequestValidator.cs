using FluentValidation;
using OrderService.Application.DTOs;

namespace OrderService.Application.Validators;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    private static readonly HashSet<string> ValidCurrencies =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "BRL", "USD", "EUR", "GBP", "JPY", "ARS", "CLP", "COP", "MXN", "PEN",
            "UYU", "BOB", "PYG", "VES", "CAD", "AUD", "CHF", "CNY", "INR", "KRW"
        };

    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("A moeda é obrigatória.")
            .Length(3).WithMessage("A moeda deve ter exatamente 3 caracteres.")
            .Must(c => ValidCurrencies.Contains(c))
            .WithMessage("Moeda inválida — use um código ISO-4217 como BRL, USD, EUR.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("O pedido deve ter ao menos um item.");

        RuleForEach(x => x.Items)
            .SetValidator(new OrderItemRequestValidator());
    }
}

public sealed class OrderItemRequestValidator : AbstractValidator<OrderItemRequest>
{
    public OrderItemRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("O id do produto é obrigatório.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("A quantidade deve ser maior que zero.");
    }
}
