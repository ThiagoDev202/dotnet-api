using FluentValidation;
using OrderService.Application.DTOs;

namespace OrderService.Application.Validators;

public sealed class TokenRequestValidator : AbstractValidator<TokenRequest>
{
    private static readonly HashSet<string> AllowedRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Customer", "Admin" };

    public TokenRequestValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("O customerId não pode ser um GUID vazio.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("O campo role é obrigatório.")
            .Must(r => AllowedRoles.Contains(r))
            .WithMessage("O campo 'role' aceita apenas 'Customer' ou 'Admin'.");
    }
}
