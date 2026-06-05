namespace OrderService.Application.DTOs;

public sealed record OrderItemRequest(
    Guid ProductId,
    int Quantity);
