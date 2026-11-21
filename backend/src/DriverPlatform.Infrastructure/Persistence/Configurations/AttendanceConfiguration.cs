using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriverPlatform.Infrastructure.Persistence.Configurations;

internal sealed class AttendanceTypeConfiguration : IEntityTypeConfiguration<AttendanceType>
{
    public void Configure(EntityTypeBuilder<AttendanceType> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(60);
        builder.Property(t => t.Code).HasMaxLength(3);
        builder.Property(t => t.Color).HasMaxLength(7);
    }
}

internal sealed class AttendanceEntryConfiguration : IEntityTypeConfiguration<AttendanceEntry>
{
    public void Configure(EntityTypeBuilder<AttendanceEntry> builder)
    {
        builder.Property(e => e.Notes).HasMaxLength(500);
        // One event per person per day.
        builder.HasIndex(e => new { e.DriverId, e.Date }).IsUnique();
        builder.HasIndex(e => e.Date);
        builder.HasOne(e => e.Driver).WithMany().OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Type).WithMany().OnDelete(DeleteBehavior.Restrict);
    }
}
