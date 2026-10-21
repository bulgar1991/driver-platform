using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriverPlatform.Infrastructure.Persistence.Configurations;

internal sealed class PartnerConfiguration : IEntityTypeConfiguration<Partner>
{
    public void Configure(EntityTypeBuilder<Partner> builder)
    {
        builder.Property(p => p.ContactEmail).HasMaxLength(254);
        builder.Property(p => p.ApiKeyPrefix).HasMaxLength(16);
        builder.Property(p => p.ApiKeyHash).HasMaxLength(64);
        builder.HasIndex(p => p.ApiKeyHash).IsUnique();
        builder.HasIndex(p => p.ApiKeyPrefix).IsUnique();
        builder.Property(p => p.Name).HasMaxLength(120);
        builder.Property(p => p.WebhookUrl).HasMaxLength(500);
        builder.Property(p => p.PriceAdjustmentPercent).HasPrecision(5, 2);
        builder.Property(p => p.CommissionPercent).HasPrecision(5, 2);
    }
}
