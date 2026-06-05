using OrderService.Application.DTOs;
using OrderService.Application.Exceptions;
using OrderService.Application.Mappings;
using OrderService.Domain.Repositories;

namespace OrderService.Application.Services;

public sealed class GetOrderService
{
    private readonly IOrderRepository _orderRepository;

    public GetOrderService(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<OrderResponse> GetByIdAsync(
        Guid orderId,
        Guid requestingCustomerId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new NotFoundException($"Pedido '{orderId}' não encontrado.");

        if (!isAdmin && order.CustomerId != requestingCustomerId)
            throw new ForbiddenException("Acesso negado.");

        return order.ToResponse();
    }
}
