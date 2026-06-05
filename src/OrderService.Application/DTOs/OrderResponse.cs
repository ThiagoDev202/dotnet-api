namespace OrderService.Application.DTOs;

public sealed record OrderResponse(
    Guid Id,
    Guid CustomerId,
    string Status,
    string Currency,
    decimal Total,
    DateTime CreatedAt,
    IReadOnlyList<OrderItemResponse> Items);
