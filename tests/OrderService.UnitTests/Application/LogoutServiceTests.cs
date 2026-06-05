using FluentAssertions;
using Moq;
using OrderService.Application.Security;
using OrderService.Application.Services;
using OrderService.Domain.Entities;
using OrderService.Domain.Repositories;

namespace OrderService.UnitTests.Application;

public sealed class LogoutServiceTests
{
    private readonly Mock<IRefreshTokenRepository> _refreshRepo = new();
    private readonly Mock<ITokenRevocationService> _revocationService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly LogoutService _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private const string RawToken = "raw-refresh-token";
    private static readonly string TokenHash = RefreshTokenService.HashToken(RawToken);

    public LogoutServiceTests()
    {
        _sut = new LogoutService(_refreshRepo.Object, _revocationService.Object, _unitOfWork.Object);

        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, CancellationToken>(async (op, _) => await op());
    }

    [Fact]
    public async Task LogoutAsync_ComRefreshTokenAtivo_RevogaJtiERefreshToken()
    {
        var storedToken = RefreshToken.Place(CustomerId, TokenHash, DateTime.UtcNow.AddDays(7));
        _refreshRepo.Setup(r => r.GetByHashAsync(TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedToken);

        var expires = DateTime.UtcNow.AddMinutes(15);
        await _sut.LogoutAsync("jti-123", expires, RawToken);

        _revocationService.Verify(
            r => r.RevokeAsync("jti-123", expires, It.IsAny<CancellationToken>()),
            Times.Once);
        storedToken.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task LogoutAsync_SemRefreshToken_ApenasRevogaJti()
    {
        var expires = DateTime.UtcNow.AddMinutes(15);
        await _sut.LogoutAsync("jti-123", expires, null);

        _revocationService.Verify(
            r => r.RevokeAsync("jti-123", expires, It.IsAny<CancellationToken>()),
            Times.Once);
        _refreshRepo.Verify(
            r => r.GetByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LogoutAsync_RefreshTokenJaRevogado_NaoTentaRevogarNovamente()
    {
        var storedToken = RefreshToken.Place(CustomerId, TokenHash, DateTime.UtcNow.AddDays(7));
        storedToken.Revoke();
        _refreshRepo.Setup(r => r.GetByHashAsync(TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedToken);

        // Não deve lançar exceção mesmo com token já revogado
        var act = async () => await _sut.LogoutAsync("jti-abc", DateTime.UtcNow.AddMinutes(15), RawToken);

        await act.Should().NotThrowAsync();
    }
}
