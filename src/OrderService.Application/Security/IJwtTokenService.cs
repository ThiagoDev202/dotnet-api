namespace OrderService.Application.Security;

public interface IJwtTokenService
{
    (string Token, string Jti, DateTime ExpiresAt) GenerateAccessToken(Guid customerId, string role);
    string GenerateRefreshTokenValue();
}
