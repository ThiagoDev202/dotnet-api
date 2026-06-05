namespace OrderService.Application.Security;

public interface ITokenRevocationService
{
    Task RevokeAsync(string jti, DateTime expiresAt, CancellationToken cancellationToken = default);
    Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default);
}
