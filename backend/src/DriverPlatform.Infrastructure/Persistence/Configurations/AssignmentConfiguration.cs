using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriverPlatform.Infrastructure.Persistence.Configurations;

internal sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.Property(a => a.DispatcherNotes).HasMaxLength(2000);
        builder.HasOne(a => a.Driver).WithMany(d => d.Assignments).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Vehicle).WithMany().OnDelete(DeleteBehavior.Restrict);
        builder.Property(a => a.CancellationReason).HasMaxLength(300);
        builder.HasIndex(a => new { a.DriverId, a.Status });
        builder.HasIndex(a => a.ShiftDate);
        builder.HasIndex(a => new { a.VehicleId, a.StartsAt });
    }
}
