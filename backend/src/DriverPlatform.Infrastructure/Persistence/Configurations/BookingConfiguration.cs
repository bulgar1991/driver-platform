using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriverPlatform.Infrastructure.Persistence.Configurations;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.Property(b => b.Reference).HasMaxLength(16);
        builder.HasIndex(b => b.Reference).IsUnique();
        builder.HasIndex(b => new { b.Status, b.PickupAt });
        builder.HasIndex(b => new { b.PartnerId, b.ExternalReference });
        builder.Property(b => b.Currency).HasMaxLength(3);
        builder.Property(b => b.ClientIp).HasMaxLength(45);
        builder.Property(b => b.FlightNumber).HasMaxLength(16);
        builder.Property(b => b.ClientNotes).HasMaxLength(2000);
        builder.Property(b => b.InternalNotes).HasMaxLength(2000);
        builder.Property(b => b.DistanceKm).HasPrecision(8, 2);
        builder.Property(b => b.SurchargePercent).HasPrecision(5, 2);
        builder.Property(b => b.VatRate).HasPrecision(5, 2);

        builder.OwnsOne(b => b.Pickup);
        builder.OwnsOne(b => b.Dropoff);
        builder.OwnsOne(b => b.PickupMeetingPoint, m => m.ToJson());
        builder.OwnsOne(b => b.DropoffMeetingPoint, m => m.ToJson());
        builder.OwnsMany(b => b.ChildSeats, s => s.ToJson());
        builder.OwnsMany(b => b.PriceLines, l => l.ToJson());

        builder.HasOne(b => b.Client).WithMany(c => c.Bookings).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Partner).WithMany().OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Assignment).WithMany(a => a.Bookings).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Booking>().WithMany().HasForeignKey(b => b.ReturnOfBookingId).OnDelete(DeleteBehavior.SetNull);
    }
}
