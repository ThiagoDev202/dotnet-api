using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Infrastructure.Persistence.Models;

namespace OrderService.Infrastructure.Persistence.Configurations;

internal sealed class RevokedTokenConfiguration : IEntityTypeConfiguration<RevokedToken>
{
    public void Configure(EntityTypeBuilder<RevokedToken> builder)
    {
        builder.ToTable("revoked_tokens");

        builder.HasKey(r => r.Jti);
        builder.Property(r => r.Jti)
            .HasColumnName("jti")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(r => r.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(r => r.RevokedAt)
            .HasColumnName("revoked_at")
            .IsRequired();

        builder.HasIndex(r => r.ExpiresAt).HasDatabaseName("ix_revoked_tokens_expires");
    }
}
