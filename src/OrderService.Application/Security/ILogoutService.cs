namespace OrderService.Application.Security;

public interface ILogoutService
{
    Task LogoutAsync(string jti, DateTime accessTokenExpiresAt, string? rawRefreshToken, CancellationToken cancellationToken = default);
}
