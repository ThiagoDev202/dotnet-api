using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products",
            t => t.HasCheckConstraint("ck_products_available_quantity", "available_quantity >= 0"));

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.UnitPrice)
            .HasColumnName("unit_price")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.AvailableQuantity)
            .HasColumnName("available_quantity")
            .IsRequired();

        // Produtos de demonstração para que a API seja utilizável logo após
        // 'docker compose up' (não há endpoint de cadastro de produto). Ids fixos
        // para que possam ser referenciados no README e em exemplos de uso.
        builder.HasData(
            Product.Create(
                new Guid("11111111-1111-1111-1111-111111111111"),
                "Caneta Azul", 10.00m, 100),
            Product.Create(
                new Guid("22222222-2222-2222-2222-222222222222"),
                "Caderno Universitário", 25.50m, 100));
    }
}
