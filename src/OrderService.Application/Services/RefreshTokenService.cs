using System.Security.Cryptography;
using System.Text;
using OrderService.Application.DTOs;
using OrderService.Application.Exceptions;
using OrderService.Application.Security;
using OrderService.Domain.Entities;
using OrderService.Domain.Repositories;

namespace OrderService.Application.Services;

public sealed class RefreshTokenService : IRefreshTokenService
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly int _refreshTokenExpirationDays;

    public RefreshTokenService(
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

    public async Task<(RefreshTokenResponse Response, string RawRefreshToken, DateTime RefreshExpiresAt)> RefreshAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
            throw new InvalidTokenException("O refresh token não pode ser vazio.");

        var hash = HashToken(rawRefreshToken);
        var stored = await _refreshTokenRepository.GetByHashAsync(hash, cancellationToken)
            ?? throw new InvalidTokenException("Refresh token inválido ou não encontrado.");

        if (!stored.IsActive)
        {
            // Detecção de roubo por replay: token já usado → revogar toda a família
            if (stored.IsRevoked)
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    await _refreshTokenRepository.RevokeAllByCustomerAsync(stored.CustomerId, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }, cancellationToken);
            }

            throw new InvalidTokenException("Refresh token já utilizado ou revogado.");
        }

        var (accessToken, _, _) = _jwtTokenService.GenerateAccessToken(stored.CustomerId, stored.Role);
        var newRaw = _jwtTokenService.GenerateRefreshTokenValue();
        var newHash = HashToken(newRaw);
        var newExpiry = DateTime.UtcNow.AddDays(_refreshTokenExpirationDays);

        RefreshToken newToken = default!;
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            newToken = stored.Replace(newHash, newExpiry);
            await _refreshTokenRepository.AddAsync(newToken, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        return (new RefreshTokenResponse(accessToken), newRaw, newExpiry);
    }

    public static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
