using System.Security.Claims;
using OrderService.Application.Security;

namespace OrderService.Api.Middleware;

public sealed class TokenBlacklistMiddleware
{
    private readonly RequestDelegate _next;

    public TokenBlacklistMiddleware(RequestDelegate next)
        => _next = next;

    public async Task InvokeAsync(HttpContext context, ITokenRevocationService revocationService)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var jti = context.User.FindFirstValue("jti")
                      ?? context.User.FindFirstValue(
                          System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti);

            if (!string.IsNullOrWhiteSpace(jti) &&
                await revocationService.IsRevokedAsync(jti, context.RequestAborted))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsync(
                    """{"status":401,"title":"Token revogado","detail":"Este token foi revogado. Faça login novamente em POST /auth/token."}""",
                    context.RequestAborted);
                return;
            }
        }

        await _next(context);
    }
}
