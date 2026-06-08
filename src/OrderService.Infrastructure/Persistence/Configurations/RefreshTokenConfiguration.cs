using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        builder.Property(r => r.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.HasIndex(r => r.TokenHash).IsUnique();
        builder.HasIndex(r => r.CustomerId).HasDatabaseName("ix_refresh_tokens_customer");
        builder.HasIndex(r => r.ExpiresAt).HasDatabaseName("ix_refresh_tokens_expires");

        builder.Property(r => r.Role)
            .HasColumnName("role")
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue("Customer");

        builder.Property(r => r.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(r => r.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(r => r.ReplacedBy)
            .HasColumnName("replaced_by")
            .HasMaxLength(128);

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
    }
}
