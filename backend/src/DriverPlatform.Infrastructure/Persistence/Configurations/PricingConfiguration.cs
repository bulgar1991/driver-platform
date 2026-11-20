using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriverPlatform.Infrastructure.Persistence.Configurations;

internal sealed class VehicleRateConfiguration : IEntityTypeConfiguration<VehicleRate>
{
    public void Configure(EntityTypeBuilder<VehicleRate> builder)
    {
        builder.HasIndex(r => r.Category).IsUnique();
        foreach (var name in new[] { nameof(VehicleRate.NameEn), nameof(VehicleRate.NameIt), nameof(VehicleRate.NameDe), nameof(VehicleRate.NameFr) })
        {
            builder.Property(name).HasMaxLength(60);
        }
        builder.Property(r => r.IsActive).HasDefaultValue(true);
        builder.Property(r => r.PricePerKm).HasPrecision(8, 2);
    }
}

internal sealed class SurchargeRuleConfiguration : IEntityTypeConfiguration<SurchargeRule>
{
    public void Configure(EntityTypeBuilder<SurchargeRule> builder)
    {
        builder.Property(r => r.Percent).HasPrecision(5, 2);
        builder.OwnsOne(r => r.NameTranslations, t => t.ToJson());
    }
}

internal sealed class ExtraFeeConfiguration : IEntityTypeConfiguration<ExtraFee>
{
    public void Configure(EntityTypeBuilder<ExtraFee> builder)
    {
        builder.HasIndex(f => f.Type).IsUnique();
    }
}

internal sealed class PricingSettingsConfiguration : IEntityTypeConfiguration<PricingSettings>
{
    public void Configure(EntityTypeBuilder<PricingSettings> builder)
    {
        builder.ToTable("pricing_settings");
        builder.Property(s => s.VatRatePercent).HasPrecision(5, 2);
        builder.Property(s => s.ReturnDiscountPercent).HasPrecision(5, 2);
    }
}
