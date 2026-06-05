using OrderService.Application.DTOs;
using OrderService.Application.Mappings;
using OrderService.Application.Repositories;

namespace OrderService.Application.Services;

public sealed class ListOrdersService
{
    private readonly IOrderQueryRepository _queryRepository;

    public ListOrdersService(IOrderQueryRepository queryRepository)
    {
        _queryRepository = queryRepository;
    }

    public async Task<PagedResult<OrderResponse>> ListAsync(
        OrderFilter filter,
        Guid requestingCustomerId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var effectiveFilter = isAdmin
            ? filter
            : filter with { CustomerId = requestingCustomerId };

        var pagedOrders = await _queryRepository.ListAsync(effectiveFilter, cancellationToken);

        var responses = pagedOrders.Items
            .Select(o => o.ToResponse())
            .ToList()
            .AsReadOnly();

        return new PagedResult<OrderResponse>(
            responses,
            pagedOrders.TotalCount,
            pagedOrders.Page,
            pagedOrders.PageSize);
    }
}
