using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace OrderService.Infrastructure.Security;

/// <summary>
/// Interceptor que executa set_config('app.current_customer_id') / set_config('app.is_admin')
/// ao iniciar cada transação, garantindo o isolamento RLS por escopo de transação.
/// Usa set_config com parâmetros posicionais — sem interpolação de string no SQL.
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
        var customerId = _currentUser.CustomerId?.ToString() ?? string.Empty;
        var isAdmin = _currentUser.IsAdmin ? "true" : "false";

        using var cmd = (NpgsqlCommand)connection.CreateCommand();
        cmd.Transaction = (NpgsqlTransaction)transaction;
        // set_config(key, value, is_local=true) equivale a SET LOCAL — escopo da transação
        cmd.CommandText =
            "SELECT set_config('app.current_customer_id', $1, true), " +
            "set_config('app.is_admin', $2, true)";
        cmd.Parameters.AddWithValue(customerId);
        cmd.Parameters.AddWithValue(isAdmin);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
