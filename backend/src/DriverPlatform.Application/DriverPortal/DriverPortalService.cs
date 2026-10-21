using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Dispatch;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Notifications;
using DriverPlatform.Application.Reviews;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.DriverPortal;

public sealed record DriverBookingView(
    Guid Id,
    string Reference,
    BookingStatus Status,
    DateTime PickupAt,
    GeoPoint Pickup,
    GeoPoint Dropoff,
    MeetingPoint? PickupMeetingPoint,
    MeetingPoint? DropoffMeetingPoint,
    int Passengers,
    int LargeLuggage,
    int SmallLuggage,
    IReadOnlyList<ChildSeatRequest> ChildSeats,
    string? FlightNumber,
    bool NameSign,
    string? NameSignText,
    string ClientName,
    string ClientPhone,
    string ClientLanguage,
    string? ClientNotes,
    decimal DistanceKm,
    int DurationMinutes,
    bool CanPickUp,
    bool CanComplete);

public sealed record DriverShiftView(
    Guid Id,
    AssignmentStatus Status,
    DateOnly ShiftDate,
    DateTime StartsAt,
    DateTime EndsAt,
    string VehicleLabel,
    string VehiclePlate,
    string? Notes,
    bool CanConfirm,
    IReadOnlyList<DriverBookingView> Bookings);

public sealed record DriverPortalResponse(string DriverFirstName, IReadOnlyList<DriverShiftView> Shifts);

public interface IDriverPortalService
{
    Task<DriverPortalResponse> GetAsync(string token, CancellationToken cancellationToken);
    Task<DriverPortalResponse> ConfirmShiftAsync(string token, Guid shiftId, CancellationToken cancellationToken);
    Task<DriverPortalResponse> PickedUpAsync(string token, Guid bookingId, CancellationToken cancellationToken);
    Task<DriverPortalResponse> CompletedAsync(string token, Guid bookingId, CancellationToken cancellationToken);
}

/// <summary>
/// The driver's personal link (business plan §5 steps 3–5): every shift sent to them, with the route,
/// people, luggage, child seats, time, client name, phone and flight; confirm, "picked up", "completed".
/// Client details stay visible only until 12 hours after the shift.
/// </summary>
public sealed class DriverPortalService(
    IAppDbContext db,
    IDriverLinks links,
    INotificationService notifications,
    IReviewService reviews,
    IAuditLogger audit,
    TimeProvider clock) : IDriverPortalService
{
    private static readonly TimeSpan KeepVisible = TimeSpan.FromHours(12);
    /// <summary>"Picked up" is accepted from this long before the pickup time, so a mis-tap days ahead cannot complete a ride.</summary>
    public static readonly TimeSpan PickupWindow = TimeSpan.FromHours(3);

    public async Task<DriverPortalResponse> GetAsync(string token, CancellationToken cancellationToken)
    {
        var driver = await FindDriverAsync(token, cancellationToken);
        var shifts = await VisibleShiftsAsync(driver, cancellationToken);

        // Opening the link counts as "seen".
        var now = clock.GetUtcNow();
        foreach (var shift in shifts.Where(s => s.Status == AssignmentStatus.Sent))
        {
            shift.Status = AssignmentStatus.Seen;
            shift.SeenAt = now;
        }
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(driver, shifts, clock.GetUtcNow());
    }

    public async Task<DriverPortalResponse> ConfirmShiftAsync(string token, Guid shiftId, CancellationToken cancellationToken)
    {
        var driver = await FindDriverAsync(token, cancellationToken);
        var shifts = await VisibleShiftsAsync(driver, cancellationToken);
        var shift = shifts.FirstOrDefault(s => s.Id == shiftId) ?? throw new NotFoundException("Shift not found.");
        if (shift.Status is not (AssignmentStatus.Sent or AssignmentStatus.Seen))
        {
            throw new ConflictException(shift.Status == AssignmentStatus.Confirmed ? "Already confirmed." : "This shift can no longer be confirmed.");
        }

        shift.Status = AssignmentStatus.Confirmed;
        shift.ConfirmedAt = clock.GetUtcNow();
        audit.LogAs($"driver {driver.FirstName} {driver.LastName}", "shift.confirmed_by_driver", nameof(Assignment), shift.Id.ToString());
        await db.SaveChangesAsync(cancellationToken);

        // Business plan §5 step 6: the client now gets the driver's and the car's details.
        await notifications.SendDriverDetailsAsync(shift, cancellationToken);
        return ToResponse(driver, shifts, clock.GetUtcNow());
    }

    public Task<DriverPortalResponse> PickedUpAsync(string token, Guid bookingId, CancellationToken cancellationToken) =>
        ChangeBookingAsync(token, bookingId, BookingStatus.PickedUp, cancellationToken);

    public Task<DriverPortalResponse> CompletedAsync(string token, Guid bookingId, CancellationToken cancellationToken) =>
        ChangeBookingAsync(token, bookingId, BookingStatus.Completed, cancellationToken);

    private async Task<DriverPortalResponse> ChangeBookingAsync(string token, Guid bookingId, BookingStatus status, CancellationToken cancellationToken)
    {
        var driver = await FindDriverAsync(token, cancellationToken);
        var shifts = await VisibleShiftsAsync(driver, cancellationToken);
        var shift = shifts.FirstOrDefault(s => s.Bookings.Any(b => b.Id == bookingId)) ?? throw new NotFoundException("Ride not found.");
        var booking = shift.Bookings.First(b => b.Id == bookingId);

        if (shift.Status != AssignmentStatus.Confirmed)
        {
            throw new ConflictException("Confirm the shift first.");
        }
        var allowed = status == BookingStatus.PickedUp ? booking.Status == BookingStatus.Assigned : booking.Status == BookingStatus.PickedUp;
        if (!allowed)
        {
            throw new ConflictException(status == BookingStatus.PickedUp ? "This ride is not waiting for pickup." : "Mark the pickup first.");
        }
        var now = clock.GetUtcNow();
        if (status == BookingStatus.PickedUp && now < booking.PickupAt - PickupWindow)
        {
            throw new ConflictException($"You can mark the pickup from {PickupWindow.TotalHours:0} hours before the pickup time.");
        }

        booking.Status = status;
        if (status == BookingStatus.PickedUp)
        {
            booking.PickedUpAt = now;
        }
        else
        {
            booking.CompletedAt = now;
        }
        await ShiftService.SyncAsync(db, shift, now, cancellationToken);
        audit.LogAs($"driver {driver.FirstName} {driver.LastName}", "booking.status_by_driver", nameof(Booking), booking.Id.ToString(),
            new { booking.Reference, To = status });
        await db.SaveChangesAsync(cancellationToken);

        if (status == BookingStatus.Completed)
        {
            await reviews.RequestAsync(booking, cancellationToken);
        }
        return ToResponse(driver, shifts, clock.GetUtcNow());
    }

    /// <summary>A wrong or regenerated link answers 404, and so does an inactive driver.</summary>
    private async Task<Driver> FindDriverAsync(string token, CancellationToken cancellationToken)
    {
        var hash = links.Hash(token);
        return await db.Drivers.FirstOrDefaultAsync(d => d.AccessTokenHash == hash && d.IsActive, cancellationToken)
            ?? throw new NotFoundException("This link is not valid. Ask the dispatcher for a new one.");
    }

    /// <summary>Shifts sent to the driver (never drafts), until 12 hours after they end.</summary>
    private async Task<List<Assignment>> VisibleShiftsAsync(Driver driver, CancellationToken cancellationToken)
    {
        var since = clock.GetUtcNow() - KeepVisible;
        return await db.Assignments
            .Include(a => a.Driver)
            .Include(a => a.Vehicle)
            .Include(a => a.Bookings).ThenInclude(b => b.Client)
            .Where(a => a.DriverId == driver.Id
                && a.Status != AssignmentStatus.Draft
                && a.EndsAt > since
                && (a.Status != AssignmentStatus.Cancelled || a.CancelledAt > since))
            .OrderBy(a => a.StartsAt)
            .ToListAsync(cancellationToken);
    }

    private static DriverPortalResponse ToResponse(Driver driver, IEnumerable<Assignment> shifts, DateTimeOffset now) => new(
        driver.FirstName,
        shifts.Select(a => new DriverShiftView(
            a.Id,
            a.Status,
            a.ShiftDate,
            ItalyTime.ToLocal(a.StartsAt),
            ItalyTime.ToLocal(a.EndsAt),
            string.Join(" ", new[] { a.Vehicle.Color, a.Vehicle.Make, a.Vehicle.Model }.Where(s => !string.IsNullOrEmpty(s))),
            a.Vehicle.PlateNumber,
            a.DispatcherNotes,
            a.Status is AssignmentStatus.Sent or AssignmentStatus.Seen,
            a.Status == AssignmentStatus.Cancelled
                ? []
                : a.Bookings.OrderBy(b => b.PickupAt).Select(b => new DriverBookingView(
                    b.Id, b.Reference, b.Status, ItalyTime.ToLocal(b.PickupAt), b.Pickup, b.Dropoff,
                    b.PickupMeetingPoint, b.DropoffMeetingPoint, b.Passengers, b.LargeLuggage, b.SmallLuggage, b.ChildSeats,
                    b.FlightNumber, b.NameSign, b.NameSignText,
                    $"{b.Client.FirstName} {b.Client.LastName}", b.Client.Phone, b.Client.Language, b.ClientNotes,
                    b.DistanceKm, b.DurationMinutes,
                    a.Status == AssignmentStatus.Confirmed && b.Status == BookingStatus.Assigned && now >= b.PickupAt - PickupWindow,
                    a.Status == AssignmentStatus.Confirmed && b.Status == BookingStatus.PickedUp)).ToList()))
        .ToList());
}
