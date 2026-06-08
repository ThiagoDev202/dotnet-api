using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OrderService.Application.DTOs;
using OrderService.Application.Security;

namespace OrderService.Api.Controllers;

[ApiController]
[Route("auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenIssuer _refreshTokenIssuer;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ILogoutService _logoutService;
    private readonly IValidator<TokenRequest> _tokenValidator;
    private readonly IWebHostEnvironment _env;

    public AuthController(
        IJwtTokenService jwtTokenService,
        IRefreshTokenIssuer refreshTokenIssuer,
        IRefreshTokenService refreshTokenService,
        ILogoutService logoutService,
        IValidator<TokenRequest> tokenValidator,
        IWebHostEnvironment env)
    {
        _jwtTokenService = jwtTokenService;
        _refreshTokenIssuer = refreshTokenIssuer;
        _refreshTokenService = refreshTokenService;
        _logoutService = logoutService;
        _tokenValidator = tokenValidator;
        _env = env;
    }

    /// <summary>
    /// [MOCK DE AUTENTICAÇÃO — apenas para fins de demonstração do teste técnico]
    /// Em produção, este endpoint deve verificar credenciais contra um Identity Provider
    /// (Keycloak, Auth0, Azure AD B2C, ASP.NET Core Identity).
    /// AVISO: Não implementa autenticação real — qualquer customerId/role é aceito.
    /// Role aceita: "Customer" ou "Admin".
    /// Emite um access token JWT (body) e um refresh token (cookie HttpOnly).
    /// O refresh token é armazenado em cookie HttpOnly+Secure+SameSite=Strict — protegido contra XSS e CSRF.
    /// </summary>
    [HttpPost("token")]
    [ProducesResponseType(typeof(AccessTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateToken(
        [FromBody] TokenRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _tokenValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            return ValidationProblem(ModelState);
        }

        var (accessToken, _, _) = _jwtTokenService.GenerateAccessToken(request.CustomerId, request.Role);
        var (rawRefresh, refreshExpiry) = await _refreshTokenIssuer.IssueAsync(
            request.CustomerId, request.Role, cancellationToken);

        SetRefreshCookie(rawRefresh, refreshExpiry);

        return Ok(new AccessTokenResponse(accessToken));
    }

    /// <summary>
    /// Renova o access token usando o refresh token do cookie HttpOnly.
    /// Rotaciona o refresh token: o anterior é invalidado e um novo cookie é definido.
    /// Se um token já usado for apresentado, toda a sessão do cliente é revogada (proteção contra replay).
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AccessTokenResponse), StatusCodes.Status200OK)]
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
        return Ok(new AccessTokenResponse(response.AccessToken));
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
