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
        builder.Property(v => v.Code).HasMaxLength(20);
        builder.Property(v => v.ChassisNumber).HasMaxLength(32);
        builder.Property(v => v.LeasingCompany).HasMaxLength(100);
        builder.Property(v => v.CostPerKm).HasPrecision(8, 3);
        builder.HasOne<Driver>().WithMany().HasForeignKey(v => v.DefaultDriverId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Photo>().WithMany().HasForeignKey(v => v.PhotoId).OnDelete(DeleteBehavior.SetNull);
    }
}
