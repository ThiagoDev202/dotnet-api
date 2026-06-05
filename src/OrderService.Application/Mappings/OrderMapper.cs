using OrderService.Application.DTOs;
using OrderService.Domain.Entities;

namespace OrderService.Application.Mappings;

internal static class OrderMapper
{
    internal static OrderResponse ToResponse(this Order order) =>
        new(
            order.Id,
            order.CustomerId,
            order.Status.ToString(),
            order.Currency,
            order.Total,
            order.CreatedAt,
            order.Items.Select(i => i.ToResponse()).ToList().AsReadOnly());

    internal static OrderItemResponse ToResponse(this OrderItem item) =>
        new(
            item.Id,
            item.ProductId,
            item.UnitPrice,
            item.Quantity,
            item.UnitPrice * item.Quantity);
}
