using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriverPlatform.Infrastructure.Persistence.Configurations;

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.Property(v => v.Notes).HasMaxLength(2000);
        builder.Property(v => v.PlateNumber).HasMaxLength(16);
        builder.HasIndex(v => v.PlateNumber).IsUnique();
    }
}
