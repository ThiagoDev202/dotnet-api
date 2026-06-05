using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Application.Repositories;
using OrderService.Application.Security;
using OrderService.Domain.Repositories;
using OrderService.Infrastructure.BackgroundServices;
using OrderService.Infrastructure.Persistence;
using OrderService.Infrastructure.Repositories;
using OrderService.Infrastructure.Security;
using OrderService.Infrastructure.UnitOfWork;

namespace OrderService.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<ICurrentUserContext, HttpCurrentUserContext>();
        services.AddSingleton<RlsInterceptor>();

        services.AddDbContext<OrderServiceDbContext>((sp, options) =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsAssembly(
                    typeof(OrderServiceDbContext).Assembly.FullName))
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<RlsInterceptor>());
        });

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderQueryRepository, OrderQueryRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ITokenRevocationService, TokenRevocationService>();
        services.AddHostedService<ExpiredTokenCleanupService>();

        return services;
    }
}
