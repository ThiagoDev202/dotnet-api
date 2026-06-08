using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;
using OrderService.Domain.Repositories;
using OrderService.Infrastructure.Persistence;

namespace OrderService.Infrastructure.Repositories;

internal sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly OrderServiceDbContext _context;

    public RefreshTokenRepository(OrderServiceDbContext context)
    {
        _context = context;
    }

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _context.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, cancellationToken);

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default) =>
        await _context.RefreshTokens.AddAsync(token, cancellationToken);

    /// <summary>
    /// Marca como revogados todos os refresh tokens ativos do cliente.
    /// <para>
    /// ⚠️ Este método NÃO chama SaveChangesAsync.
    /// O caller é responsável por persistir as mudanças dentro de uma transação
    /// usando <c>IUnitOfWork.ExecuteInTransactionAsync</c> seguido de <c>SaveChangesAsync</c>.
    /// </para>
    /// </summary>
    public async Task RevokeAllByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var tokens = await _context.RefreshTokens
            .Where(r => r.CustomerId == customerId && r.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
            token.Revoke();
    }
}
