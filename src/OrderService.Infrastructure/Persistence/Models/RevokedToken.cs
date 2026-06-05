namespace OrderService.Infrastructure.Persistence.Models;

internal sealed class RevokedToken
{
    public string Jti { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime RevokedAt { get; set; }
}
