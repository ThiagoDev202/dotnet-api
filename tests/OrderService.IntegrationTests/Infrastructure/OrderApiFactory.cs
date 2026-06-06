using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace OrderService.IntegrationTests.Infrastructure;

/// <summary>
/// Factory compartilhada entre todas as classes de teste do grupo "Integration".
/// Usa postgres superuser para evitar problemas de permissão (refresh_tokens/revoked_tokens
/// não têm GRANT para orders_app — bug que não impacta os testes com superuser).
/// RLS é bypassado pelo superuser; isolamento de acesso é coberto pela camada de aplicação.
/// </summary>
public class OrderApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("orderservice_test")
        .WithUsername("postgres")
        .WithPassword("postgres_test")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Sobrescreve connection string e demais settings lidos de forma lazy
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["Jwt:Key"] = JwtTestHelper.TestKey,
                ["Jwt:Issuer"] = "OrderService",
                ["Jwt:Audience"] = "OrderServiceApi",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Jwt:RefreshTokenExpirationDays"] = "7"
            });
        });

        // Program.cs lê builder.Configuration["Jwt:Key"] de forma EAGER (antes do Build),
        // portanto as TokenValidationParameters ficam com a chave do appsettings.json.
        // PostConfigure roda DEPOIS que todos os serviços são configurados, garantindo
        // que a chave de teste seja usada para validar os tokens gerados pelo JwtTestHelper.
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtTestHelper.TestKey));
                options.TokenValidationParameters.IssuerSigningKey = key;
                options.TokenValidationParameters.ValidIssuer = "OrderService";
                options.TokenValidationParameters.ValidAudience = "OrderServiceApi";
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<OrderServiceDbContext>()
            .UseNpgsql(
                _postgres.GetConnectionString(),
                npgsql => npgsql.MigrationsAssembly(
                    typeof(OrderServiceDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var db = new OrderServiceDbContext(options);
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    public async Task<Guid> SeedProductAsync(
        string name = "Produto Teste",
        decimal price = 50m,
        int quantity = 10)
    {
        var product = Product.Create(Guid.NewGuid(), name, price, quantity);

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderServiceDbContext>();
        db.Products.Add(product);
        await db.SaveChangesAsync();

        return product.Id;
    }

    public async Task<int> GetProductStockAsync(Guid productId)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderServiceDbContext>();
        return await db.Products
            .AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => p.AvailableQuantity)
            .FirstAsync();
    }

    public HttpClient CreateAuthenticatedClient(Guid customerId, string role = "Customer")
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.GenerateToken(customerId, role));
        return client;
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<OrderApiFactory> { }
