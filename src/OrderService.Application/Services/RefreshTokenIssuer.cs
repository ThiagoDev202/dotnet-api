using OrderService.Application.Security;
using OrderService.Domain.Entities;
using OrderService.Domain.Repositories;

namespace OrderService.Application.Services;

public sealed class RefreshTokenIssuer : IRefreshTokenIssuer
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly int _refreshTokenExpirationDays;

    public RefreshTokenIssuer(
        IRefreshTokenRepository refreshTokenRepository,
        IJwtTokenService jwtTokenService,
        IUnitOfWork unitOfWork,
        int refreshTokenExpirationDays)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _jwtTokenService = jwtTokenService;
        _unitOfWork = unitOfWork;
        _refreshTokenExpirationDays = refreshTokenExpirationDays;
    }

    public async Task<(string RawToken, DateTime ExpiresAt)> IssueAsync(
        Guid customerId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var rawToken = _jwtTokenService.GenerateRefreshTokenValue();
        var hash = RefreshTokenService.HashToken(rawToken);
        var expiresAt = DateTime.UtcNow.AddDays(_refreshTokenExpirationDays);

        var token = RefreshToken.Place(customerId, hash, expiresAt, role);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _refreshTokenRepository.AddAsync(token, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        return (rawToken, expiresAt);
    }
}
