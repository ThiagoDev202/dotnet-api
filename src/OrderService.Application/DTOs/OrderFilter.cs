using OrderService.Domain.Enums;

namespace OrderService.Application.DTOs;

public sealed record OrderFilter(
    Guid? CustomerId = null,
    OrderStatus? Status = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 20);
