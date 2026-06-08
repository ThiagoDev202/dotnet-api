using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Domain.Entities;
using OrderService.Domain.ValueObjects;

namespace OrderService.Infrastructure.Persistence.Configurations;

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");

        builder.Property(i => i.ProductId).HasColumnName("product_id").IsRequired();

        builder.OwnsOne(i => i.UnitPrice, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("unit_price")
                .HasPrecision(18, 2)
                .IsRequired();
            money.Property(m => m.Currency)
                .HasColumnName("unit_price_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(i => i.Quantity)
            .HasColumnName("quantity")
            .HasConversion(q => q.Value, v => Quantity.Of(v))
            .IsRequired();

        // FK shadow property para order_id
        builder.Property<Guid>("OrderId").HasColumnName("order_id");
    }
}
