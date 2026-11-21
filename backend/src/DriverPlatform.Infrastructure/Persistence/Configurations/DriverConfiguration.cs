using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriverPlatform.Infrastructure.Persistence.Configurations;

internal sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.Property(d => d.Notes).HasMaxLength(2000);
        builder.Property(d => d.Phone).HasMaxLength(32);
        builder.Property(d => d.Email).HasMaxLength(254);
        builder.Property(d => d.AccessTokenHash).HasMaxLength(64);
        builder.HasIndex(d => d.AccessTokenHash).IsUnique();
        builder.Property(d => d.CommissionPercent).HasPrecision(5, 2);
        builder.Property(d => d.EmployeeNumber).HasMaxLength(20);
        builder.Property(d => d.TaxCode).HasMaxLength(32);
        builder.Property(d => d.PlaceOfBirth).HasMaxLength(100);
        builder.Property(d => d.JobFunction).HasMaxLength(60);
        builder.Property(d => d.Address).HasMaxLength(200);
        builder.Property(d => d.HomePhone).HasMaxLength(32);
        builder.Property(d => d.MaxDrivingHours).HasPrecision(4, 1);
        builder.HasMany(d => d.Documents).WithOne(doc => doc.Driver).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Photo>().WithMany().HasForeignKey(d => d.PhotoId).OnDelete(DeleteBehavior.SetNull);
    }
}
