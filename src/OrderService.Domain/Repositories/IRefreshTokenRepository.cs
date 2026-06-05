using OrderService.Domain.Entities;

namespace OrderService.Domain.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task RevokeAllByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
}
