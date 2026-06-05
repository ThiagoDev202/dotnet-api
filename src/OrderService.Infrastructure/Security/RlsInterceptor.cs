using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace OrderService.Infrastructure.Security;

/// <summary>
/// Interceptor que executa SET LOCAL app.current_customer_id / app.is_admin
/// ao iniciar cada transação, garantindo o isolamento RLS por escopo de transação.
/// SET LOCAL (não SET) evita vazamento no pool de conexões.
/// </summary>
public sealed class RlsInterceptor : DbTransactionInterceptor
{
    private readonly ICurrentUserContext _currentUser;

    public RlsInterceptor(ICurrentUserContext currentUser)
        => _currentUser = currentUser;

    public override async ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        await ApplyRlsAsync(connection, result, cancellationToken);
        return result;
    }

    public override DbTransaction TransactionStarted(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result)
    {
        ApplyRlsAsync(connection, result, CancellationToken.None)
            .GetAwaiter().GetResult();
        return result;
    }

    private async Task ApplyRlsAsync(
        DbConnection connection, DbTransaction transaction, CancellationToken ct)
    {
        // Guid.ToString() é sempre seguro (sem caracteres especiais)
        var customerId = _currentUser.CustomerId?.ToString() ?? string.Empty;
        var isAdmin = _currentUser.IsAdmin ? "true" : "false";

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = $"""
            SET LOCAL app.current_customer_id = '{customerId}';
            SET LOCAL app.is_admin = '{isAdmin}';
            """;

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
