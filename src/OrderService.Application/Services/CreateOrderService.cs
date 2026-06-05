using OrderService.Application.DTOs;
using OrderService.Application.Exceptions;
using OrderService.Application.Mappings;
using OrderService.Domain.Entities;
using OrderService.Domain.Exceptions;
using OrderService.Domain.Repositories;

namespace OrderService.Application.Services;

public sealed class CreateOrderService
{
    private readonly IProductRepository _productRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateOrderService(
        IProductRepository productRepository,
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<OrderResponse> CreateAsync(
        CreateOrderRequest request,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _productRepository.GetByIdsAsync(productIds, cancellationToken);
        var productMap = products.ToDictionary(p => p.Id);

        var items = new List<OrderItem>();
        foreach (var itemRequest in request.Items)
        {
            if (!productMap.TryGetValue(itemRequest.ProductId, out var product))
                throw new NotFoundException($"Produto '{itemRequest.ProductId}' não encontrado.");

            if (!product.HasSufficientStock(itemRequest.Quantity))
                throw new DomainException($"Estoque insuficiente para o produto '{itemRequest.ProductId}'.");

            items.Add(OrderItem.Create(itemRequest.ProductId, product.UnitPrice, itemRequest.Quantity));
        }

        var order = Order.Place(customerId, request.Currency, items);
        await _orderRepository.AddAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return order.ToResponse();
    }
}
