namespace OrderService.Application.Exceptions;

public sealed class InvalidTokenException(string message) : Exception(message);
