using OrderService.Application.DTOs;
using OrderService.Application.Exceptions;
using OrderService.Application.Mappings;
using OrderService.Domain.Enums;
using OrderService.Domain.Repositories;

namespace OrderService.Application.Services;

public sealed class CancelOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelOrderService(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<OrderResponse> CancelAsync(
        Guid orderId,
        Guid requestingCustomerId,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new NotFoundException($"Pedido '{orderId}' não encontrado.");

        if (order.CustomerId != requestingCustomerId)
            throw new ForbiddenException("Acesso negado.");

        if (order.Status == OrderStatus.Canceled)
            return order.ToResponse();

        var wasConfirmed = order.Status == OrderStatus.Confirmed;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            order.Cancel();

            if (wasConfirmed)
            {
                foreach (var item in order.Items)
                    await _productRepository.ReleaseStockAsync(item.ProductId, item.Quantity, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        return order.ToResponse();
    }
}
