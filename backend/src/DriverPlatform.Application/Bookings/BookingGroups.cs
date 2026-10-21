using DriverPlatform.Application.Abstractions;
using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Bookings;

public static class BookingGroups
{
    /// <summary>The outbound booking and its return leg, if any: they are paid and invoiced together.</summary>
    public static async Task<List<Booking>> LoadLegsAsync(this IAppDbContext db, Booking outbound, CancellationToken cancellationToken)
    {
        var legs = new List<Booking> { outbound };
        legs.AddRange(await db.Bookings
            .Where(b => b.ReturnOfBookingId == outbound.Id)
            .ToListAsync(cancellationToken));
        return legs;
    }
}
