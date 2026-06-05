using Microsoft.EntityFrameworkCore;
using OrderService.Application.DTOs;
using OrderService.Application.Repositories;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Persistence;

namespace OrderService.Infrastructure.Repositories;

internal sealed class OrderQueryRepository : IOrderQueryRepository
{
    private readonly OrderServiceDbContext _context;

    public OrderQueryRepository(OrderServiceDbContext context) => _context = context;

    public async Task<PagedResult<Order>> ListAsync(
        OrderFilter filter,
        CancellationToken cancellationToken = default)
    {
        // Inicia transação explícita para garantir RLS SET LOCAL via interceptor
        if (_context.Database.CurrentTransaction is not null)
            return await ExecuteListAsync(filter, cancellationToken);

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        var result = await ExecuteListAsync(filter, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<PagedResult<Order>> ExecuteListAsync(
        OrderFilter filter, CancellationToken ct)
    {
        var query = _context.Orders
            .Include(o => o.Items)
            .AsNoTracking();

        if (filter.CustomerId.HasValue)
            query = query.Where(o => o.CustomerId == filter.CustomerId.Value);

        if (filter.Status.HasValue)
            query = query.Where(o => o.Status == filter.Status.Value);

        if (filter.From.HasValue)
            query = query.Where(o => o.CreatedAt >= filter.From.Value);

        if (filter.To.HasValue)
            query = query.Where(o => o.CreatedAt <= filter.To.Value);

        var totalCount = await query.CountAsync(ct);

        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 20 : filter.PageSize;

        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<Order>(items, totalCount, page, pageSize);
    }
}
