using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderService.Infrastructure.Persistence;

namespace OrderService.Infrastructure.BackgroundServices;

public sealed class ExpiredTokenCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredTokenCleanupService> _logger;

    public ExpiredTokenCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExpiredTokenCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CleanupAsync(stoppingToken);
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<OrderServiceDbContext>();
            var now = DateTime.UtcNow;

            var revokedDeleted = await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM revoked_tokens WHERE expires_at < {0}",
                new object[] { now }, ct);

            var refreshDeleted = await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM refresh_tokens WHERE expires_at < {0} AND revoked_at IS NOT NULL",
                new object[] { now }, ct);

            if (revokedDeleted > 0 || refreshDeleted > 0)
                _logger.LogInformation(
                    "Limpeza de tokens expirados: {RevokedCount} revoked_tokens, {RefreshCount} refresh_tokens removidos.",
                    revokedDeleted, refreshDeleted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Erro durante limpeza de tokens expirados.");
        }
    }
}
