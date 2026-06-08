using OrderService.Application.DTOs;

namespace OrderService.Application.Security;

public interface IRefreshTokenService
{
    Task<(RefreshTokenResponse Response, string RawRefreshToken, DateTime RefreshExpiresAt)> RefreshAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default);
}
