using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderService.Infrastructure.Persistence;

/// <summary>
/// Permite executar `dotnet ef migrations add` apontando apenas para o projeto Infrastructure,
/// sem necessidade de configurar o projeto Api como startup em tempo de design.
/// </summary>
internal sealed class OrderServiceDbContextFactory
    : IDesignTimeDbContextFactory<OrderServiceDbContext>
{
    public OrderServiceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OrderServiceDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=orderservice_dev;Username=postgres;Password=postgres",
                npgsql => npgsql.MigrationsAssembly(
                    typeof(OrderServiceDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new OrderServiceDbContext(options);
    }
}
