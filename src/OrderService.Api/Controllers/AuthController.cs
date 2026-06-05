using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Application.DTOs;
using OrderService.Application.Security;
using OrderService.Application.Services;

namespace OrderService.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenIssuer _refreshTokenIssuer;
    private readonly RefreshTokenService _refreshTokenService;
    private readonly LogoutService _logoutService;
    private readonly IWebHostEnvironment _env;

    public AuthController(
        IJwtTokenService jwtTokenService,
        IRefreshTokenIssuer refreshTokenIssuer,
        RefreshTokenService refreshTokenService,
        LogoutService logoutService,
        IWebHostEnvironment env)
    {
        _jwtTokenService = jwtTokenService;
        _refreshTokenIssuer = refreshTokenIssuer;
        _refreshTokenService = refreshTokenService;
        _logoutService = logoutService;
        _env = env;
    }

    /// <summary>
    /// Emite um access token JWT (body) e um refresh token (cookie HttpOnly).
    /// Role aceita: "Customer" ou "Admin".
    /// O refresh token é armazenado em cookie HttpOnly+Secure+SameSite=Strict — protegido contra XSS e CSRF.
    /// </summary>
    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateToken(
        [FromBody] TokenRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CustomerId == Guid.Empty)
            return ValidationProblem(new ValidationProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Dados inválidos",
                Detail = "O campo 'customerId' é obrigatório e não pode ser o GUID vazio.",
                Errors = { ["customerId"] = ["O customerId não pode ser um GUID vazio (all-zeros)."] }
            });

        if (string.IsNullOrWhiteSpace(request.Role))
            return ValidationProblem(new ValidationProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Dados inválidos",
                Detail = "O campo 'role' é obrigatório. Valores aceitos: 'Customer', 'Admin'.",
                Errors = { ["role"] = ["O campo role é obrigatório."] }
            });

        var (accessToken, _, _) = _jwtTokenService.GenerateAccessToken(request.CustomerId, request.Role);
        var (rawRefresh, refreshExpiry) = await _refreshTokenIssuer.IssueAsync(request.CustomerId, cancellationToken);

        SetRefreshCookie(rawRefresh, refreshExpiry);

        return Ok(new TokenResponse(accessToken));
    }

    /// <summary>
    /// Renova o access token usando o refresh token do cookie HttpOnly.
    /// Rotaciona o refresh token: o anterior é invalidado e um novo cookie é definido.
    /// Se um token já usado for apresentado, toda a sessão do cliente é revogada (proteção contra replay).
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var rawRefresh = Request.Cookies["refresh_token"];
        if (string.IsNullOrWhiteSpace(rawRefresh))
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Refresh token ausente",
                Detail = "O cookie 'refresh_token' não foi encontrado. Faça login novamente em POST /auth/token."
            });

        var (response, newRaw, newExpiry) = await _refreshTokenService.RefreshAsync(rawRefresh, cancellationToken);

        SetRefreshCookie(newRaw, newExpiry);
        return Ok(new TokenResponse(response.AccessToken));
    }

    /// <summary>
    /// Revoga o access token atual (blacklist por jti) e invalida o refresh token do cookie.
    /// Após o logout, o cookie refresh_token é apagado e o token não pode ser renovado.
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var jti = User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti);

        if (string.IsNullOrWhiteSpace(jti))
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Token inválido",
                Detail = "O token não contém o claim 'jti' necessário para revogação."
            });

        var expClaim = User.FindFirstValue("exp");
        var expiresAt = expClaim is not null
            ? DateTimeOffset.FromUnixTimeSeconds(long.Parse(expClaim)).UtcDateTime
            : DateTime.UtcNow.AddMinutes(15);

        var rawRefresh = Request.Cookies["refresh_token"];

        await _logoutService.LogoutAsync(jti, expiresAt, rawRefresh, cancellationToken);

        Response.Cookies.Delete("refresh_token");
        return NoContent();
    }

    private void SetRefreshCookie(string rawRefreshToken, DateTime expiresAt)
    {
        Response.Cookies.Append("refresh_token", rawRefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !_env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Expires = new DateTimeOffset(expiresAt),
            Path = "/"
        });
    }
}

public sealed record TokenRequest(Guid CustomerId, string Role);
public sealed record TokenResponse(string Token);
