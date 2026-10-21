using DriverPlatform.Application.Abstractions;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Bookings;

public interface IHousekeepingService
{
    /// <summary>Cancels website bookings never paid; returns how many.</summary>
    Task<int> CancelUnpaidAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Unpaid bookings would otherwise stay "awaiting payment" forever. They are cancelled 48 hours after
/// booking, or one hour before pickup, unless a payment page is still open.
/// </summary>
public sealed class HousekeepingService(IAppDbContext db, IAuditLogger audit, TimeProvider clock) : IHousekeepingService
{
    public static readonly TimeSpan PayWithin = TimeSpan.FromHours(48);
    public static readonly TimeSpan BeforePickup = TimeSpan.FromHours(1);

    public async Task<int> CancelUnpaidAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var stale = await db.Bookings
            .Where(b => b.Status == BookingStatus.AwaitingPayment
                && (b.CreatedAt < now - PayWithin || b.PickupAt < now + BeforePickup)
                && !b.Payments.Any(p => p.Status == PaymentStatus.Pending && p.ExpiresAt > now)
                && !db.Bookings.Any(o => o.Id == b.ReturnOfBookingId
                    && o.Payments.Any(p => p.Status == PaymentStatus.Pending && p.ExpiresAt > now)))
            .ToListAsync(cancellationToken);

        foreach (var booking in stale)
        {
            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = now;
            booking.CancellationReason = "Not paid";
            audit.Log("booking.cancelled_unpaid", "Booking", booking.Id.ToString(), new { booking.Reference });
        }
        if (stale.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        return stale.Count;
    }
}
