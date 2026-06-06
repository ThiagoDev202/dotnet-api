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
/// Igual à <see cref="OrderApiFactory"/>, mas o runtime conecta com a role restrita
/// orders_app (RLS ENFORCED), exatamente como em produção (docker-compose). As migrations
/// rodam como superuser (DDL privilegiado). Usada para validar o isolamento real no banco:
/// acesso cross-tenant não retorna 403 (app), e sim 404 — a linha fica invisível pelo RLS.
/// </summary>
public class RlsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string AppRole = "orders_app";
    private const string AppPassword = "orders_app_password";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("orderservice_test")
        .WithUsername("postgres")
        .WithPassword("postgres_test")
        .Build();

    private string SuperuserConnection => _postgres.GetConnectionString();

    private string AppConnection =>
        $"Host={_postgres.Hostname};Port={_postgres.GetMappedPublicPort(5432)};" +
        $"Database=orderservice_test;Username={AppRole};Password={AppPassword}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Runtime: role restrita sujeita ao RLS. Migração: superuser para o DDL.
                ["ConnectionStrings:DefaultConnection"] = AppConnection,
                ["ConnectionStrings:MigrationConnection"] = SuperuserConnection,
                ["Jwt:Key"] = JwtTestHelper.TestKey,
                ["Jwt:Issuer"] = "OrderService",
                ["Jwt:Audience"] = "OrderServiceApi",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Jwt:RefreshTokenExpirationDays"] = "7"
            });
        });

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

        // Migrations como superuser: cria schema, role orders_app, RLS, GRANTs e seed.
        var options = new DbContextOptionsBuilder<OrderServiceDbContext>()
            .UseNpgsql(
                SuperuserConnection,
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
        // products não tem RLS; orders_app tem GRANT — seed funciona pela conexão de runtime.
        var product = Product.Create(Guid.NewGuid(), name, price, quantity);

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderServiceDbContext>();
        db.Products.Add(product);
        await db.SaveChangesAsync();

        return product.Id;
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

[CollectionDefinition("RlsIntegration")]
public class RlsIntegrationCollection : ICollectionFixture<RlsApiFactory> { }
