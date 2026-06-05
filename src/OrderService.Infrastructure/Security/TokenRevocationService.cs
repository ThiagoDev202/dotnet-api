using Microsoft.EntityFrameworkCore;
using OrderService.Application.Security;
using OrderService.Infrastructure.Persistence;
using OrderService.Infrastructure.Persistence.Models;

namespace OrderService.Infrastructure.Security;

internal sealed class TokenRevocationService : ITokenRevocationService
{
    private readonly OrderServiceDbContext _context;

    public TokenRevocationService(OrderServiceDbContext context)
    {
        _context = context;
    }

    public async Task RevokeAsync(string jti, DateTime expiresAt, CancellationToken cancellationToken = default)
    {
        var exists = await _context.RevokedTokens
            .AnyAsync(r => r.Jti == jti, cancellationToken);

        if (!exists)
        {
            await _context.RevokedTokens.AddAsync(new RevokedToken
            {
                Jti = jti,
                ExpiresAt = expiresAt,
                RevokedAt = DateTime.UtcNow
            }, cancellationToken);
        }
    }

    public Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default) =>
        _context.RevokedTokens.AnyAsync(r => r.Jti == jti, cancellationToken);
}
