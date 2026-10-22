using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Notifications;
using DriverPlatform.Application.Reviews;
using DriverPlatform.Application.Pricing;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using DriverPlatform.Application.Localization;

namespace DriverPlatform.Application.Dispatch;

public interface IShiftService
{
    Task<IReadOnlyList<ShiftDto>> ListAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<ShiftDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<ShiftResult> CreateAsync(ShiftRequest request, CancellationToken cancellationToken);
    Task<ShiftResult> UpdateAsync(Guid id, ShiftRequest request, CancellationToken cancellationToken);
    Task<ShiftDto> ChangeStatusAsync(Guid id, ShiftStatusRequest request, CancellationToken cancellationToken);
    Task<AvailabilityResponse> AvailabilityAsync(IReadOnlyList<Guid> bookingIds, Guid? shiftId, CancellationToken cancellationToken);
}

/// <summary>
/// Business plan §5: the dispatcher builds a shift — driver, car and one or more bookings. Hard rules
/// (car too small, documents expired, driver or car busy) block the save; softer ones come back as warnings.
/// </summary>
public sealed class ShiftService(
    IAppDbContext db,
    INotificationService notifications,
    IReviewService reviews,
    IAuditLogger audit,
    TimeProvider clock) : IShiftService
{
    /// <summary>Time kept free between two shifts of the same driver or car.</summary>
    public static readonly TimeSpan Buffer = TimeSpan.FromMinutes(30);

    private static readonly AssignmentStatus[] Open =
        [AssignmentStatus.Draft, AssignmentStatus.Sent, AssignmentStatus.Seen, AssignmentStatus.Confirmed];

    public async Task<IReadOnlyList<ShiftDto>> ListAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var shifts = await Query().AsNoTracking()
            .Where(a => a.ShiftDate >= from && a.ShiftDate <= to)
            .OrderBy(a => a.StartsAt)
            .ToListAsync(cancellationToken);
        var messages = await MessagesAsync(shifts.Select(s => s.Id).ToList(), cancellationToken);
        return shifts.Select(s => ToDto(s, messages)).ToList();
    }

    public async Task<ShiftDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await ToDtoAsync(await FindAsync(id, cancellationToken), cancellationToken);

    public Task<ShiftResult> CreateAsync(ShiftRequest request, CancellationToken cancellationToken) =>
        SaveAsync(new Assignment { Status = AssignmentStatus.Draft }, request, isNew: true, cancellationToken);

    public async Task<ShiftResult> UpdateAsync(Guid id, ShiftRequest request, CancellationToken cancellationToken)
    {
        var shift = await FindAsync(id, cancellationToken);
        if (!Open.Contains(shift.Status))
        {
            throw new ConflictException(Texts.Current.ShiftLocked(shift.Status));
        }
        return await SaveAsync(shift, request, isNew: false, cancellationToken);
    }

    public async Task<ShiftDto> ChangeStatusAsync(Guid id, ShiftStatusRequest request, CancellationToken cancellationToken)
    {
        var shift = await FindAsync(id, cancellationToken);
        var now = clock.GetUtcNow();
        var bookings = shift.Bookings.ToList();

        var previous = shift.Status;
        NotificationKind? driverMessage = null;
        var completedNow = new List<Booking>();

        switch (request.Status)
        {
            // Sends (or re-sends) the shift to the driver on WhatsApp and email with the personal link.
            case AssignmentStatus.Sent when shift.Status is AssignmentStatus.Draft or AssignmentStatus.Sent or AssignmentStatus.Seen:
                shift.SentAt = now;
                driverMessage = NotificationKind.DriverShift;
                break;
            // Normally the driver confirms on the link; the dispatcher can record a confirmation by phone.
            case AssignmentStatus.Confirmed when shift.Status is AssignmentStatus.Draft or AssignmentStatus.Sent or AssignmentStatus.Seen:
                shift.ConfirmedAt = now;
                break;
            case AssignmentStatus.Draft when shift.Status is AssignmentStatus.Sent or AssignmentStatus.Seen or AssignmentStatus.Confirmed:
                shift.ConfirmedAt = null;
                break;
            case AssignmentStatus.Completed when Open.Contains(shift.Status):
                foreach (var booking in bookings.Where(b => b.Status is BookingStatus.Assigned or BookingStatus.PickedUp))
                {
                    booking.Status = BookingStatus.Completed;
                    booking.PickedUpAt ??= now;
                    booking.CompletedAt = now;
                    completedNow.Add(booking);
                }
                shift.CompletedAt = now;
                break;
            case AssignmentStatus.Cancelled when Open.Contains(shift.Status):
                if (string.IsNullOrWhiteSpace(request.Reason))
                {
                    throw new RequestValidationException("reason", Texts.Current.SayWhyShiftCancelled);
                }
                if (bookings.Any(b => b.Status is BookingStatus.PickedUp or BookingStatus.Completed))
                {
                    throw new ConflictException(Texts.Current.AlreadyPickedUp);
                }
                // The rides themselves still happen: they go back to the list of bookings to assign.
                foreach (var booking in bookings.Where(b => b.Status == BookingStatus.Assigned))
                {
                    booking.Status = BookingStatus.New;
                    booking.AssignmentId = null;
                    booking.Assignment = null;
                }
                shift.CancelledAt = now;
                shift.CancellationReason = request.Reason.Trim();
                if (shift.SentAt is not null)
                {
                    driverMessage = NotificationKind.DriverShiftChanged;
                }
                break;
            default:
                throw new RequestValidationException("status", Texts.Current.ShiftStatusChange(shift.Status, request.Status));
        }

        shift.Status = request.Status;
        audit.Log("shift.status_changed", nameof(Assignment), shift.Id.ToString(),
            new { From = previous, To = request.Status, request.Reason });
        await db.SaveChangesAsync(cancellationToken);

        if (driverMessage is { } kind)
        {
            await notifications.SendShiftToDriverAsync(shift, kind, cancellationToken);
        }
        if (request.Status == AssignmentStatus.Confirmed)
        {
            await notifications.SendDriverDetailsAsync(shift, cancellationToken);
        }
        foreach (var booking in completedNow)
        {
            await reviews.RequestAsync(booking, cancellationToken);
        }
        return await ToDtoAsync(await FindAsync(id, cancellationToken), cancellationToken);
    }

    public async Task<AvailabilityResponse> AvailabilityAsync(IReadOnlyList<Guid> bookingIds, Guid? shiftId, CancellationToken cancellationToken)
    {
        var bookings = await db.Bookings.AsNoTracking().Where(b => bookingIds.Contains(b.Id)).ToListAsync(cancellationToken);
        if (bookings.Count == 0)
        {
            return new AvailabilityResponse([], []);
        }
        var (start, end, lastDate) = Window(bookings);
        var busy = await BusyAsync(start, end, shiftId, cancellationToken);

        var drivers = await db.Drivers.AsNoTracking().Include(d => d.Documents).Where(d => d.IsActive).ToListAsync(cancellationToken);
        var vehicles = await db.Vehicles.AsNoTracking().Where(v => v.IsActive).ToListAsync(cancellationToken);

        var driverOptions = drivers.Select(d =>
            {
                var problems = DriverProblems(d, lastDate, busy);
                return new DriverOption(d.Id, $"{d.FirstName} {d.LastName}", problems.Count == 0, problems);
            })
            .OrderByDescending(o => o.Available).ThenBy(o => o.Name).ToList();
        var vehicleOptions = vehicles.Select(v =>
            {
                var problems = VehicleProblems(v, bookings, lastDate, busy);
                return new VehicleOption(v.Id, $"{v.Make} {v.Model}", v.PlateNumber, v.Category, problems.Count == 0, problems);
            })
            // Smallest car that fits first, so vans stay free for groups.
            .OrderByDescending(o => o.Available).ThenBy(o => o.Category).ThenBy(o => o.PlateNumber).ToList();

        return new AvailabilityResponse(driverOptions, vehicleOptions);
    }

    /// <summary>
    /// Keeps the shift in step with its bookings after one of them changes: new time window,
    /// and completed or cancelled once every booking is.
    /// </summary>
    internal static async Task SyncAsync(IAppDbContext db, Assignment shift, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var bookings = await db.Bookings.Where(b => b.AssignmentId == shift.Id).ToListAsync(cancellationToken);
        var active = bookings.Where(b => b.Status != BookingStatus.Cancelled).ToList();
        if (active.Count == 0)
        {
            shift.Status = AssignmentStatus.Cancelled;
            shift.CancelledAt = now;
            shift.CancellationReason = "All bookings cancelled";
            return;
        }
        (shift.StartsAt, shift.EndsAt, _) = Window(active);
        shift.ShiftDate = DateOnly.FromDateTime(ItalyTime.ToLocal(shift.StartsAt));
        if (active.All(b => b.Status == BookingStatus.Completed) && Open.Contains(shift.Status))
        {
            shift.Status = AssignmentStatus.Completed;
            shift.CompletedAt = now;
        }
    }

    private async Task<ShiftResult> SaveAsync(Assignment shift, ShiftRequest request, bool isNew, CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        var warnings = new List<string>();

        var driver = await db.Drivers.Include(d => d.Documents).FirstOrDefaultAsync(d => d.Id == request.DriverId, cancellationToken);
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == request.VehicleId, cancellationToken);
        var ids = request.BookingIds.Distinct().ToList();
        var bookings = await db.Bookings.Include(b => b.Client).Where(b => ids.Contains(b.Id)).ToListAsync(cancellationToken);

        if (driver is not { IsActive: true })
        {
            errors.Add("driverId", Texts.Current.ChooseActiveDriver);
        }
        if (vehicle is not { IsActive: true })
        {
            errors.Add("vehicleId", Texts.Current.ChooseActiveVehicle);
        }
        if (bookings.Count != ids.Count || ids.Count == 0)
        {
            errors.Add("bookingIds", Texts.Current.ChooseBookings);
        }
        foreach (var booking in bookings)
        {
            var mine = !isNew && booking.AssignmentId == shift.Id;
            if (!(booking.Status == BookingStatus.New && booking.AssignmentId is null) && !(mine && booking.Status == BookingStatus.Assigned))
            {
                errors.Add("bookingIds", booking.Status == BookingStatus.AwaitingPayment
                    ? $"{booking.Reference} is not paid yet."
                    : $"{booking.Reference} is {booking.Status.ToString().ToLowerInvariant()} and cannot be assigned.");
            }
        }
        errors.ThrowIfAny();

        var ordered = bookings.OrderBy(b => b.PickupAt).ToList();
        var (start, end, lastDate) = Window(ordered);
        var busy = await BusyAsync(start, end, isNew ? null : shift.Id, cancellationToken);

        foreach (var problem in DriverProblems(driver!, lastDate, busy))
        {
            errors.Add("driverId", problem);
        }
        foreach (var problem in VehicleProblems(vehicle!, ordered, lastDate, busy))
        {
            errors.Add("vehicleId", problem);
        }
        for (var i = 1; i < ordered.Count; i++)
        {
            var previousEnd = ordered[i - 1].PickupAt.AddMinutes(ordered[i - 1].DurationMinutes);
            if (ordered[i].PickupAt < previousEnd)
            {
                errors.Add("bookingIds", Texts.Current.StartsBeforeEnds(ordered[i].Reference, ordered[i - 1].Reference));
            }
            else if (ordered[i].PickupAt < previousEnd + Buffer)
            {
                warnings.Add(Texts.Current.TightGap((int)(ordered[i].PickupAt - previousEnd).TotalMinutes, ordered[i - 1].Reference, ordered[i].Reference));
            }
        }
        errors.ThrowIfAny();

        var today = DateOnly.FromDateTime(ItalyTime.ToLocal(clock.GetUtcNow()));
        warnings.AddRange(DocumentRules.Missing(driver!).Select(m => $"{driver!.FirstName} {driver.LastName}: {m}"));
        warnings.AddRange(DocumentRules.Missing(vehicle!).Select(m => $"{vehicle!.PlateNumber}: {m}"));
        warnings.AddRange(DocumentRules.Dated(driver!).Where(d => d.ExpiresOn <= today.AddDays(DocumentRules.AlertDays))
            .Select(d => $"{driver!.FirstName} {driver.LastName}: {d.Document} expires on {d.ExpiresOn:yyyy-MM-dd}"));
        warnings.AddRange(DocumentRules.Dated(vehicle!).Where(d => d.ExpiresOn <= today.AddDays(DocumentRules.AlertDays))
            .Select(d => $"{vehicle!.PlateNumber}: {d.Document} expires on {d.ExpiresOn:yyyy-MM-dd}"));

        // Bookings taken out of the shift go back to the list to assign.
        if (!isNew)
        {
            foreach (var removed in await db.Bookings.Where(b => b.AssignmentId == shift.Id && !ids.Contains(b.Id)).ToListAsync(cancellationToken))
            {
                removed.AssignmentId = null;
                removed.Status = BookingStatus.New;
            }
        }

        shift.DriverId = driver!.Id;
        shift.VehicleId = vehicle!.Id;
        shift.DispatcherNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        shift.StartsAt = start;
        shift.EndsAt = end;
        shift.ShiftDate = DateOnly.FromDateTime(ItalyTime.ToLocal(start));
        // A changed shift needs confirming again. If the driver already had it, the updated link is sent at once.
        var resend = !isNew && shift.SentAt is not null && shift.Status != AssignmentStatus.Draft;
        if (!isNew && shift.Status != AssignmentStatus.Draft)
        {
            shift.Status = resend ? AssignmentStatus.Sent : AssignmentStatus.Draft;
            shift.ConfirmedAt = null;
            if (resend)
            {
                shift.SentAt = clock.GetUtcNow();
            }
        }
        if (isNew)
        {
            db.Assignments.Add(shift);
        }
        foreach (var booking in ordered)
        {
            booking.AssignmentId = shift.Id;
            booking.Status = BookingStatus.Assigned;
        }

        audit.Log(isNew ? "shift.created" : "shift.updated", nameof(Assignment), shift.Id.ToString(), new
        {
            Driver = $"{driver.FirstName} {driver.LastName}",
            vehicle.PlateNumber,
            Bookings = ordered.Select(b => b.Reference),
        });
        await db.SaveChangesAsync(cancellationToken);

        var saved = await FindAsync(shift.Id, cancellationToken);
        if (resend)
        {
            await notifications.SendShiftToDriverAsync(saved, NotificationKind.DriverShiftChanged, cancellationToken);
        }
        return new ShiftResult(await ToDtoAsync(saved, cancellationToken), warnings);
    }

    private sealed record Busy(HashSet<Guid> Drivers, HashSet<Guid> Vehicles);

    /// <summary>Drivers and cars already in an open shift that overlaps [start, end], with the buffer on both sides.</summary>
    private async Task<Busy> BusyAsync(DateTimeOffset start, DateTimeOffset end, Guid? exceptShiftId, CancellationToken cancellationToken)
    {
        var from = start - Buffer;
        var to = end + Buffer;
        var overlapping = await db.Assignments.AsNoTracking()
            .Where(a => a.Id != exceptShiftId && Open.Contains(a.Status) && a.StartsAt < to && a.EndsAt > from)
            .Select(a => new { a.DriverId, a.VehicleId })
            .ToListAsync(cancellationToken);
        return new Busy(overlapping.Select(a => a.DriverId).ToHashSet(), overlapping.Select(a => a.VehicleId).ToHashSet());
    }

    private static List<string> DriverProblems(Driver driver, DateOnly lastDate, Busy busy)
    {
        var problems = DocumentRules.ExpiredOn(driver, lastDate);
        if (!driver.IsAvailable)
        {
            problems.Add(Texts.Current.MarkedNotAvailable);
        }
        if (busy.Drivers.Contains(driver.Id))
        {
            problems.Add(Texts.Current.OnAnotherShift);
        }
        return problems;
    }

    /// <summary>A bigger category may serve a smaller one (a van can do a sedan ride), never the other way.</summary>
    private static List<string> VehicleProblems(Vehicle vehicle, IReadOnlyList<Booking> bookings, DateOnly lastDate, Busy busy)
    {
        var problems = DocumentRules.ExpiredOn(vehicle, lastDate);
        foreach (var b in bookings)
        {
            if (vehicle.Category < b.VehicleCategory)
            {
                problems.Add($"{b.Reference} was booked as {b.VehicleCategory}");
            }
            if (b.Passengers > vehicle.Seats)
            {
                problems.Add($"{b.Reference}: {b.Passengers} passengers, {vehicle.Seats} seats");
            }
            var luggage = TripCapacity.LuggageUnits(b.LargeLuggage, b.SmallLuggage);
            if (luggage > vehicle.LuggageCapacity)
            {
                problems.Add($"{b.Reference}: luggage does not fit");
            }
            var seats = b.ChildSeats.Sum(s => s.Count);
            if (seats > vehicle.ChildSeatsAvailable)
            {
                problems.Add($"{b.Reference}: needs {seats} child seat(s), car has {vehicle.ChildSeatsAvailable}");
            }
        }
        if (busy.Vehicles.Contains(vehicle.Id))
        {
            problems.Add(Texts.Current.OnAnotherShift);
        }
        return problems.Distinct().ToList();
    }

    private static (DateTimeOffset Start, DateTimeOffset End, DateOnly LastDate) Window(IReadOnlyCollection<Booking> bookings)
    {
        var start = bookings.Min(b => b.PickupAt);
        var end = bookings.Max(b => b.PickupAt.AddMinutes(b.DurationMinutes));
        return (start, end, DateOnly.FromDateTime(ItalyTime.ToLocal(end)));
    }

    private IQueryable<Assignment> Query() => db.Assignments
        .Include(a => a.Driver)
        .Include(a => a.Vehicle)
        .Include(a => a.Bookings).ThenInclude(b => b.Client)
        .Include(a => a.Bookings).ThenInclude(b => b.Partner);

    private async Task<Assignment> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await Query().FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
        ?? throw new NotFoundException(Texts.Current.ShiftNotFound);

    private async Task<ShiftDto> ToDtoAsync(Assignment shift, CancellationToken cancellationToken) =>
        ToDto(shift, await MessagesAsync([shift.Id], cancellationToken));

    /// <summary>Latest message per channel to the driver, for the shift card.</summary>
    private async Task<ILookup<Guid, MessageSummary>> MessagesAsync(List<Guid> shiftIds, CancellationToken cancellationToken)
    {
        var logs = await db.NotificationLogs.AsNoTracking()
            .Where(n => n.AssignmentId != null && shiftIds.Contains(n.AssignmentId.Value))
            .OrderByDescending(n => n.Id)
            .ToListAsync(cancellationToken);
        return logs
            .GroupBy(n => (n.AssignmentId!.Value, n.Channel))
            .Select(g => g.First())
            .ToLookup(n => n.AssignmentId!.Value, n => new MessageSummary(n.Kind, n.Channel, n.Status, n.Recipient, n.Error, n.CreatedAt));
    }

    private static ShiftDto ToDto(Assignment a, ILookup<Guid, MessageSummary> messages) => new(
        a.Id,
        a.Status,
        a.ShiftDate,
        ItalyTime.ToLocal(a.StartsAt),
        ItalyTime.ToLocal(a.EndsAt),
        a.DriverId,
        $"{a.Driver.FirstName} {a.Driver.LastName}",
        a.Driver.Phone,
        a.VehicleId,
        $"{a.Vehicle.Make} {a.Vehicle.Model}",
        a.Vehicle.PlateNumber,
        a.Vehicle.Category,
        a.DispatcherNotes,
        a.Bookings.OrderBy(b => b.PickupAt).Select(b =>
        {
            // Navigation back to the shift is needed by the list item mapping.
            b.Assignment = a;
            return b.ToListItem();
        }).ToList(),
        a.SentAt,
        a.SeenAt,
        a.ConfirmedAt,
        a.CompletedAt,
        messages[a.Id].ToList());
}
