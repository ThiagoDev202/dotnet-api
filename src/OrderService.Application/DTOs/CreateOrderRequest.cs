namespace OrderService.Application.DTOs;

public sealed record CreateOrderRequest(
    string Currency,
    IReadOnlyList<OrderItemRequest> Items);
