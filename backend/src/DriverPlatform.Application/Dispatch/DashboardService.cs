using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Geo;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Dispatch;

public interface IDashboardService
{
    Task<DashboardResponse> GetAsync(CancellationToken cancellationToken);
}

public sealed class DashboardService(IAppDbContext db, TimeProvider clock) : IDashboardService
{
    public async Task<DashboardResponse> GetAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(ItalyTime.ToLocal(now));
        var dayStart = ItalyTime.ToUtc(today.ToDateTime(TimeOnly.MinValue))!.Value;
        var dayEnd = ItalyTime.ToUtc(today.AddDays(1).ToDateTime(TimeOnly.MinValue))!.Value;
        var horizon = now.AddHours(48);

        var todays = db.Bookings.Where(b => b.PickupAt >= dayStart && b.PickupAt < dayEnd
            && b.Status != BookingStatus.AwaitingPayment && b.Status != BookingStatus.Cancelled);
        var unassigned = db.Bookings.Where(b => b.Status == BookingStatus.New && b.AssignmentId == null && b.PickupAt < horizon);

        var nextUnassigned = await unassigned
            .Include(b => b.Client)
            .Include(b => b.Partner)
            .OrderBy(b => b.PickupAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        var drivers = await db.Drivers.AsNoTracking().Include(d => d.Documents).Where(d => d.IsActive).ToListAsync(cancellationToken);
        var vehicles = await db.Vehicles.AsNoTracking().Where(v => v.IsActive).ToListAsync(cancellationToken);
        var limit = today.AddDays(DocumentRules.AlertDays);
        var alerts = drivers
            .SelectMany(d => DocumentRules.Dated(d).Select(doc => new DocumentAlert($"{d.FirstName} {d.LastName}", "Driver", d.Id, doc.Document, doc.ExpiresOn, doc.ExpiresOn < today)))
            .Concat(vehicles.SelectMany(v => DocumentRules.Dated(v).Select(doc => new DocumentAlert($"{v.PlateNumber} · {v.Make} {v.Model}", "Vehicle", v.Id, doc.Document, doc.ExpiresOn, doc.ExpiresOn < today))))
            .Where(a => a.ExpiresOn <= limit)
            .OrderBy(a => a.ExpiresOn)
            .ToList();

        return new DashboardResponse(
            await todays.CountAsync(cancellationToken),
            await todays.CountAsync(b => b.Status == BookingStatus.Completed, cancellationToken),
            await db.Bookings.CountAsync(b => b.Status == BookingStatus.PickedUp, cancellationToken),
            await unassigned.CountAsync(cancellationToken),
            await db.Bookings.CountAsync(b => b.Status == BookingStatus.AwaitingPayment && b.PickupAt > now, cancellationToken),
            await db.Assignments.CountAsync(a => (a.Status == AssignmentStatus.Draft || a.Status == AssignmentStatus.Sent || a.Status == AssignmentStatus.Seen) && a.EndsAt > now, cancellationToken),
            nextUnassigned.Select(b => b.ToListItem()).ToList(),
            alerts);
    }
}
