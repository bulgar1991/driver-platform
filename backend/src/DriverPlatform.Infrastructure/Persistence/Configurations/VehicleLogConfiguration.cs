using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriverPlatform.Infrastructure.Persistence.Configurations;

internal sealed class VehicleExpirationConfiguration : IEntityTypeConfiguration<VehicleExpiration>
{
    public void Configure(EntityTypeBuilder<VehicleExpiration> builder)
    {
        builder.Property(e => e.Title).HasMaxLength(80);
        builder.HasOne(e => e.Vehicle).WithMany(v => v.Expirations).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class VehicleRefuelConfiguration : IEntityTypeConfiguration<VehicleRefuel>
{
    public void Configure(EntityTypeBuilder<VehicleRefuel> builder)
    {
        builder.Property(r => r.Station).HasMaxLength(80);
        builder.Property(r => r.Liters).HasPrecision(8, 2);
        builder.HasIndex(r => new { r.VehicleId, r.Date });
        builder.HasOne(r => r.Vehicle).WithMany(v => v.Refuels).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(r => r.Driver).WithMany().OnDelete(DeleteBehavior.SetNull);
    }
}
