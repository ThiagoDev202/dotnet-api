using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Application.DTOs;
using OrderService.Application.Services;
using OrderService.Domain.Enums;

namespace OrderService.Api.Controllers;

[ApiController]
[Route("orders")]
[Authorize]
public sealed class OrdersController : ControllerBase
{
    private readonly CreateOrderService _createService;
    private readonly ConfirmOrderService _confirmService;
    private readonly CancelOrderService _cancelService;
    private readonly GetOrderService _getService;
    private readonly ListOrdersService _listService;
    private readonly IValidator<CreateOrderRequest> _validator;

    public OrdersController(
        CreateOrderService createService,
        ConfirmOrderService confirmService,
        CancelOrderService cancelService,
        GetOrderService getService,
        ListOrdersService listService,
        IValidator<CreateOrderRequest> validator)
    {
        _createService = createService;
        _confirmService = confirmService;
        _cancelService = cancelService;
        _getService = getService;
        _listService = listService;
        _validator = validator;
    }

    private Guid RequestingCustomerId
    {
        get
        {
            if (!Guid.TryParse(User.FindFirstValue("customerId"), out var id))
                throw new UnauthorizedAccessException("Token não contém claim 'customerId' válido.");
            return id;
        }
    }

    private bool IsAdmin =>
        User.IsInRole("Admin") || User.FindFirstValue("role") == "Admin";

    /// <summary>
    /// Cria um novo pedido para o cliente autenticado.
    /// O customerId é extraído do token JWT — não é possível criar pedidos em nome de outro cliente.
    /// Retorna 422 se um produto não existir ou o estoque for insuficiente.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            return ValidationProblem(ModelState);
        }

        var response = await _createService.CreateAsync(request, RequestingCustomerId, cancellationToken);
        return CreatedAtAction(nameof(GetOrder), new { id = response.Id }, response);
    }

    /// <summary>
    /// Confirma um pedido no status Placed, baixando o estoque atomicamente.
    /// Operação idempotente: chamar novamente em um pedido já Confirmed retorna 200 sem efeito colateral.
    /// Retorna 409 em caso de conflito de concorrência (xmin).
    /// </summary>
    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmOrder(Guid id, CancellationToken cancellationToken)
    {
        var response = await _confirmService.ConfirmAsync(id, RequestingCustomerId, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Cancela um pedido (Placed ou Confirmed), devolvendo o estoque se estava Confirmed.
    /// Operação idempotente: chamar novamente em um pedido já Canceled retorna 200 sem efeito colateral.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CancelOrder(Guid id, CancellationToken cancellationToken)
    {
        var response = await _cancelService.CancelAsync(id, RequestingCustomerId, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Retorna um pedido pelo id.
    /// Clientes só visualizam os próprios pedidos; Admin visualiza qualquer pedido.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrder(Guid id, CancellationToken cancellationToken)
    {
        var response = await _getService.GetByIdAsync(id, RequestingCustomerId, IsAdmin, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Lista pedidos com filtros e paginação.
    /// Clientes veem apenas os próprios pedidos (filtro customerId ignorado).
    /// Admin pode filtrar por qualquer customerId.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListOrders(
        [FromQuery] Guid? customerId,
        [FromQuery] OrderStatus? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var filter = new OrderFilter(customerId, status, from, to, page, pageSize);
        var result = await _listService.ListAsync(filter, RequestingCustomerId, IsAdmin, cancellationToken);
        return Ok(result);
    }
}
