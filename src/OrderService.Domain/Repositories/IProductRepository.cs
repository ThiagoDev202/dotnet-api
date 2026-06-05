using OrderService.Domain.Entities;

namespace OrderService.Domain.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<bool> TryDecrementStockAsync(Guid productId, int quantity, CancellationToken cancellationToken = default);
    Task ReleaseStockAsync(Guid productId, int quantity, CancellationToken cancellationToken = default);
}
