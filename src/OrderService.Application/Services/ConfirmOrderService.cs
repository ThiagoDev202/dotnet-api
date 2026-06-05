using OrderService.Application.DTOs;
using OrderService.Application.Exceptions;
using OrderService.Application.Mappings;
using OrderService.Domain.Enums;
using OrderService.Domain.Exceptions;
using OrderService.Domain.Repositories;

namespace OrderService.Application.Services;

public sealed class ConfirmOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmOrderService(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<OrderResponse> ConfirmAsync(
        Guid orderId,
        Guid requestingCustomerId,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new NotFoundException($"Pedido '{orderId}' não encontrado.");

        if (order.CustomerId != requestingCustomerId)
            throw new ForbiddenException("Acesso negado.");

        if (order.Status == OrderStatus.Confirmed)
            return order.ToResponse();

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Valida a transição de status antes de tocar no estoque
            order.Confirm();

            foreach (var item in order.Items)
            {
                var success = await _productRepository.TryDecrementStockAsync(
                    item.ProductId, item.Quantity, cancellationToken);

                if (!success)
                    throw new DomainException($"Estoque insuficiente para o produto '{item.ProductId}'.");
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        return order.ToResponse();
    }
}
