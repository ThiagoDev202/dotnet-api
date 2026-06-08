using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderService.Infrastructure.Persistence;

/// <summary>
/// Permite executar `dotnet ef migrations add` apontando apenas para o projeto Infrastructure,
/// sem necessidade de configurar o projeto Api como startup em tempo de design.
/// Requer a variável de ambiente MIGRATION_CONNECTION com a connection string do superuser.
/// </summary>
internal sealed class OrderServiceDbContextFactory
    : IDesignTimeDbContextFactory<OrderServiceDbContext>
{
    public OrderServiceDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("MIGRATION_CONNECTION")
            ?? throw new InvalidOperationException(
                "Defina a variável de ambiente MIGRATION_CONNECTION para rodar migrations. " +
                "Exemplo: export MIGRATION_CONNECTION=" +
                "'Host=localhost;Database=orderservice;Username=postgres;Password=<sua-senha>'");

        var options = new DbContextOptionsBuilder<OrderServiceDbContext>()
            .UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsAssembly(
                    typeof(OrderServiceDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new OrderServiceDbContext(options);
    }
}
