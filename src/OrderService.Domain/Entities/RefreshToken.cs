using OrderService.Domain.Exceptions;

namespace OrderService.Domain.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public string Role { get; private set; } = "Customer";
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? ReplacedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => !IsRevoked && !IsExpired;

    private static readonly HashSet<string> AllowedRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Customer", "Admin" };

    private RefreshToken() { }

    public static RefreshToken Place(Guid customerId, string tokenHash, DateTime expiresAt, string role = "Customer")
    {
        if (customerId == Guid.Empty)
            throw new DomainException("O id do cliente é obrigatório.");
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new DomainException("O hash do token não pode ser vazio.");
        if (expiresAt <= DateTime.UtcNow)
            throw new DomainException("A data de expiração deve ser futura.");
        if (string.IsNullOrWhiteSpace(role) || !AllowedRoles.Contains(role))
            throw new DomainException($"Role inválida. Valores aceitos: {string.Join(", ", AllowedRoles)}.");

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            TokenHash = tokenHash,
            Role = role,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Revoke()
    {
        if (IsRevoked)
            throw new DomainException("O token já foi revogado.");

        RevokedAt = DateTime.UtcNow;
    }

    public RefreshToken Replace(string newTokenHash, DateTime newExpiresAt)
    {
        if (IsRevoked)
            throw new DomainException("Não é possível substituir um token já revogado.");
        if (IsExpired)
            throw new DomainException("Não é possível substituir um token expirado.");

        RevokedAt = DateTime.UtcNow;
        ReplacedBy = newTokenHash;

        return Place(CustomerId, newTokenHash, newExpiresAt, Role);
    }
}
