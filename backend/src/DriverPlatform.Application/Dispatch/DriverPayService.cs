using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Localization;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Dispatch;

public interface IDriverPayService
{
    Task<decimal?> SetAsync(Guid bookingId, decimal? amount, CancellationToken cancellationToken);
    Task<IReadOnlyList<DriverPayLine>> MonthAsync(int year, int month, CancellationToken cancellationToken);
}

/// <summary>Drivers are paid a fixed amount per ride; the month's salary is the sum over their completed rides.</summary>
public sealed class DriverPayService(IAppDbContext db, IAuditLogger audit) : IDriverPayService
{
    public async Task<decimal?> SetAsync(Guid bookingId, decimal? amount, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken)
            ?? throw new NotFoundException(Texts.Current.BookingNotFound);
        if (booking.AssignmentId is null)
        {
            throw new RequestValidationException("amount", Texts.Current.BookingNotOnShift);
        }
        var before = booking.DriverPay;
        booking.DriverPay = amount is { } value ? Math.Round(value, 2, MidpointRounding.AwayFromZero) : null;

        audit.Log("booking.driver_pay_set", nameof(Booking), bookingId.ToString(), new { booking.Reference, before, after = booking.DriverPay });
        await db.SaveChangesAsync(cancellationToken);
        return booking.DriverPay;
    }

    public async Task<IReadOnlyList<DriverPayLine>> MonthAsync(int year, int month, CancellationToken cancellationToken)
    {
        var (from, to) = ItalyTime.Month(year, month);
        var rides = await db.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.Completed && b.Assignment != null && b.PickupAt >= from && b.PickupAt < to)
            .Select(b => new
            {
                b.Assignment!.DriverId,
                b.Assignment.Driver.FirstName,
                b.Assignment.Driver.LastName,
                b.DistanceKm,
                b.DriverPay,
            })
            .ToListAsync(cancellationToken);

        return rides
            .GroupBy(r => r.DriverId)
            .Select(g => new DriverPayLine(
                g.Key,
                $"{g.First().FirstName} {g.First().LastName}",
                g.Count(),
                g.Sum(r => r.DistanceKm),
                g.Sum(r => r.DriverPay ?? 0),
                g.Count(r => r.DriverPay is null)))
            .OrderBy(l => l.DriverName)
            .ToList();
    }
}
