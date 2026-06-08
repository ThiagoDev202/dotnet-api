namespace OrderService.Application.DTOs;

public sealed record TokenRequest(Guid CustomerId, string Role);
