namespace OrderService.Application.Security;

public interface IRefreshTokenIssuer
{
    Task<(string RawToken, DateTime ExpiresAt)> IssueAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);
}
