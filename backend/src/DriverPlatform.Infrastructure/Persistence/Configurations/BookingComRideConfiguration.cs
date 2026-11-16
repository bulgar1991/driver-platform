using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriverPlatform.Infrastructure.Persistence.Configurations;

internal sealed class BookingComRideConfiguration : IEntityTypeConfiguration<BookingComRide>
{
    public void Configure(EntityTypeBuilder<BookingComRide> builder)
    {
        builder.Property(r => r.CustomerReference).HasMaxLength(64);
        builder.Property(r => r.BookingReference).HasMaxLength(64);
        builder.HasIndex(r => r.BookingReference).IsUnique();
        builder.Property(r => r.Status).HasMaxLength(32);
        builder.Property(r => r.StateHash).HasMaxLength(128);
        builder.Property(r => r.DetailsHash).HasMaxLength(64);
        builder.Property(r => r.DriverSentKey).HasMaxLength(80);
        builder.Property(r => r.LastError).HasMaxLength(500);
        builder.HasIndex(r => r.BookingId).IsUnique();
        builder.HasOne(r => r.Booking).WithMany().HasForeignKey(r => r.BookingId).OnDelete(DeleteBehavior.Cascade);
    }
}
