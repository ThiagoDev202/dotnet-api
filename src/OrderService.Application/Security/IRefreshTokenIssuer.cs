namespace OrderService.Application.Security;

public interface IRefreshTokenIssuer
{
    Task<(string RawToken, DateTime ExpiresAt)> IssueAsync(
        Guid customerId,
        string role,
        CancellationToken cancellationToken = default);
}
