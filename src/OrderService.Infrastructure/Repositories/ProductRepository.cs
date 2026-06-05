using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;
using OrderService.Domain.Repositories;
using OrderService.Infrastructure.Persistence;

namespace OrderService.Infrastructure.Repositories;

internal sealed class ProductRepository : IProductRepository
{
    private readonly OrderServiceDbContext _context;

    public ProductRepository(OrderServiceDbContext context) => _context = context;

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Products.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        return await _context.Products
            .Where(p => idList.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Decremento atômico condicional: retorna false se estoque insuficiente (0 linhas afetadas).
    /// Deve ser chamado dentro de uma transação (ExecuteInTransactionAsync).
    /// </summary>
    public async Task<bool> TryDecrementStockAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        var affected = await _context.Database.ExecuteSqlRawAsync(
            """
            UPDATE products
               SET available_quantity = available_quantity - {0}
             WHERE id = {1}
               AND available_quantity >= {0}
            """,
            quantity, productId,
            cancellationToken);

        return affected > 0;
    }

    public async Task ReleaseStockAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        await _context.Database.ExecuteSqlRawAsync(
            """
            UPDATE products
               SET available_quantity = available_quantity + {0}
             WHERE id = {1}
            """,
            quantity, productId,
            cancellationToken);
    }
}
