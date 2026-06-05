namespace OrderService.Application.Security;

public interface IJwtTokenService
{
    string GenerateToken(Guid customerId, string role);
}
