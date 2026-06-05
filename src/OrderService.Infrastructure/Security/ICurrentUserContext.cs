namespace OrderService.Infrastructure.Security;

public interface ICurrentUserContext
{
    Guid? CustomerId { get; }
    bool IsAdmin { get; }
}
