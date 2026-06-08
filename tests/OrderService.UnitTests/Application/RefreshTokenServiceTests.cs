using FluentAssertions;
using Moq;
using OrderService.Application.Exceptions;
using OrderService.Application.Security;
using OrderService.Application.Services;
using OrderService.Domain.Entities;
using OrderService.Domain.Repositories;

namespace OrderService.UnitTests.Application;

public sealed class RefreshTokenServiceTests
{
    private readonly Mock<IRefreshTokenRepository> _refreshRepo = new();
    private readonly Mock<IJwtTokenService> _jwtService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly RefreshTokenService _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private const string RawToken = "raw-token-value";
    private static readonly string TokenHash = RefreshTokenService.HashToken(RawToken);

    public RefreshTokenServiceTests()
    {
        _sut = new RefreshTokenService(_refreshRepo.Object, _jwtService.Object, _unitOfWork.Object, refreshTokenExpirationDays: 7);

        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, CancellationToken>(async (op, _) => await op());

        _jwtService
            .Setup(j => j.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(("access-token", "jti-abc", DateTime.UtcNow.AddMinutes(15)));

        _jwtService
            .Setup(j => j.GenerateRefreshTokenValue())
            .Returns("new-raw-refresh");
    }

    private static RefreshToken BuildActiveToken() =>
        RefreshToken.Place(CustomerId, TokenHash, DateTime.UtcNow.AddDays(7));

    // ─── Token válido ────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshAsync_TokenAtivo_RetornaNovoAccessTokenERotaciona()
    {
        var stored = BuildActiveToken();
        _refreshRepo.Setup(r => r.GetByHashAsync(TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);

        var (response, newRaw, expiry) = await _sut.RefreshAsync(RawToken);

        response.AccessToken.Should().Be("access-token");
        newRaw.Should().Be("new-raw-refresh");
        expiry.Should().BeAfter(DateTime.UtcNow);
        _refreshRepo.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
        stored.IsRevoked.Should().BeTrue();
    }

    // ─── Token inexistente ───────────────────────────────────────────────────

    [Fact]
    public async Task RefreshAsync_TokenNaoEncontrado_LancaInvalidTokenException()
    {
        _refreshRepo.Setup(r => r.GetByHashAsync(TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);

        var act = async () => await _sut.RefreshAsync(RawToken);

        await act.Should().ThrowAsync<InvalidTokenException>().WithMessage("*inválido*");
    }

    // ─── Token revogado (replay) ─────────────────────────────────────────────

    [Fact]
    public async Task RefreshAsync_TokenRevogado_RevogaFamiliaELancaInvalidTokenException()
    {
        var stored = BuildActiveToken();
        stored.Revoke();
        _refreshRepo.Setup(r => r.GetByHashAsync(TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);

        var act = async () => await _sut.RefreshAsync(RawToken);

        await act.Should().ThrowAsync<InvalidTokenException>().WithMessage("*revogado*");
        _refreshRepo.Verify(
            r => r.RevokeAllByCustomerAsync(CustomerId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── Raw token vazio ─────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshAsync_RawTokenVazio_LancaInvalidTokenException()
    {
        var act = async () => await _sut.RefreshAsync("   ");

        await act.Should().ThrowAsync<InvalidTokenException>().WithMessage("*vazio*");
    }

    // ─── Hash determinístico ─────────────────────────────────────────────────

    [Fact]
    public void HashToken_MesmaEntrada_ProduzesMesmoHash()
    {
        var h1 = RefreshTokenService.HashToken("token-abc");
        var h2 = RefreshTokenService.HashToken("token-abc");

        h1.Should().Be(h2);
    }

    [Fact]
    public void HashToken_EntradasDiferentes_ProduzeHashesDiferentes()
    {
        var h1 = RefreshTokenService.HashToken("token-abc");
        var h2 = RefreshTokenService.HashToken("token-xyz");

        h1.Should().NotBe(h2);
    }
}
