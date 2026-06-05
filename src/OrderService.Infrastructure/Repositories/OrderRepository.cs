using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;
using OrderService.Domain.Repositories;
using OrderService.Infrastructure.Persistence;

namespace OrderService.Infrastructure.Repositories;

internal sealed class OrderRepository : IOrderRepository
{
    private readonly OrderServiceDbContext _context;

    public OrderRepository(OrderServiceDbContext context) => _context = context;

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        await _context.Orders.AddAsync(order, cancellationToken);
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Se já existe transação ativa (ex.: dentro de ExecuteInTransactionAsync), reutiliza
        if (_context.Database.CurrentTransaction is not null)
        {
            return await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        }

        // Inicia transação explícita para que o RlsInterceptor execute SET LOCAL
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return order;
    }
}
