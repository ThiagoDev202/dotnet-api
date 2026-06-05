using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OrderService.Application.Security;

namespace OrderService.Infrastructure.Security;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly string _key;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expirationMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        var section = configuration.GetSection("Jwt");
        _key = section["Key"]!;
        _issuer = section["Issuer"]!;
        _audience = section["Audience"]!;
        _expirationMinutes = section.GetValue("ExpirationMinutes", 15);
    }

    public (string Token, string Jti, DateTime ExpiresAt) GenerateAccessToken(Guid customerId, string role)
    {
        var jti = Guid.NewGuid().ToString();
        var expiresAt = DateTime.UtcNow.AddMinutes(_expirationMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim("customerId", customerId.ToString()),
            new Claim("role", role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), jti, expiresAt);
    }

    public string GenerateRefreshTokenValue()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }
}
