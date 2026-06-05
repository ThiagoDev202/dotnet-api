using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Persistence.Models;

namespace OrderService.Infrastructure.Persistence;

public sealed class OrderServiceDbContext : DbContext
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    internal DbSet<RevokedToken> RevokedTokens => Set<RevokedToken>();

    public OrderServiceDbContext(DbContextOptions<OrderServiceDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderServiceDbContext).Assembly);
    }
}
