using OrderService.Application.DTOs;
using OrderService.Domain.Entities;

namespace OrderService.Application.Repositories;

public interface IOrderQueryRepository
{
    Task<PagedResult<Order>> ListAsync(OrderFilter filter, CancellationToken cancellationToken = default);
}
