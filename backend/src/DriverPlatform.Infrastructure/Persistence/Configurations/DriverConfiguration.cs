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
        builder.HasMany(d => d.Documents).WithOne(doc => doc.Driver).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Photo>().WithMany().HasForeignKey(d => d.PhotoId).OnDelete(DeleteBehavior.SetNull);
    }
}
