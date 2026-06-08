using OrderService.Application.Security;
using OrderService.Domain.Repositories;

namespace OrderService.Application.Services;

public sealed class LogoutService : ILogoutService
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenRevocationService _revocationService;
    private readonly IUnitOfWork _unitOfWork;

    public LogoutService(
        IRefreshTokenRepository refreshTokenRepository,
        ITokenRevocationService revocationService,
        IUnitOfWork unitOfWork)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _revocationService = revocationService;
        _unitOfWork = unitOfWork;
    }

    public async Task LogoutAsync(
        string jti,
        DateTime accessTokenExpiresAt,
        string? rawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Revoga o jti do access token (blacklist)
            await _revocationService.RevokeAsync(jti, accessTokenExpiresAt, cancellationToken);

            // Revoga o refresh token se presente
            if (!string.IsNullOrWhiteSpace(rawRefreshToken))
            {
                var hash = RefreshTokenService.HashToken(rawRefreshToken);
                var stored = await _refreshTokenRepository.GetByHashAsync(hash, cancellationToken);
                if (stored is not null && !stored.IsRevoked)
                    stored.Revoke();
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
    }
}
