using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Localization;
using DriverPlatform.Application.Pricing;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Dispatch;

/// <summary>A ride on the services calendar.</summary>
/// <param name="Start">Start in the office time zone: where the bar sits on the timeline.</param>
/// <param name="PickupLocal">Pickup in the local time of the pickup, for labels (differs abroad).</param>
/// <param name="DriverPay">Admins only; null for dispatchers or when not set.</param>
public sealed record CalendarRide(
    Guid Id,
    string Reference,
    BookingStatus Status,
    BookingSource Source,
    DateTime Start,
    DateTime PickupLocal,
    int DurationMinutes,
    string PickupLabel,
    string DropoffLabel,
    Guid ClientId,
    string ClientName,
    Guid? PartnerId,
    string? PartnerName,
    int Passengers,
    int LargeLuggage,
    int SmallLuggage,
    int ChildSeats,
    VehicleCategory VehicleCategory,
    decimal DistanceKm,
    decimal TotalPrice,
    decimal? DriverPay,
    Guid? AssignmentId,
    AssignmentStatus? ShiftStatus,
    Guid? DriverId,
    string? DriverName,
    Guid? VehicleId,
    string? VehiclePlate,
    string? FlightNumber);

public sealed record CalendarVehicle(Guid Id, string PlateNumber, string? Code, string Name, VehicleCategory Category, int Seats, bool IsActive);

public sealed record CalendarDriver(Guid Id, string Name, bool IsActive, bool IsAvailable);

/// <summary>An attendance day of a driver, drawn behind their row.</summary>
public sealed record CalendarAbsence(Guid DriverId, DateOnly Date, string Name, string Code, string Color, bool MakesUnavailable, TimeOnly? StartTime, TimeOnly? EndTime);

public sealed record CalendarResponse(
    DateOnly From,
    DateOnly To,
    string Currency,
    IReadOnlyList<CalendarRide> Rides,
    IReadOnlyList<CalendarVehicle> Vehicles,
    IReadOnlyList<CalendarDriver> Drivers,
    IReadOnlyList<CalendarAbsence> Absences);

public interface ICalendarService
{
    Task<CalendarResponse> GetAsync(DateOnly from, DateOnly to, bool includePay, CancellationToken cancellationToken);
}

/// <summary>
/// The services calendar: every ride of the period (not the ones still waiting for payment) with its driver and
/// car, the fleet as rows, and the drivers' attendance days.
/// </summary>
public sealed class CalendarService(IAppDbContext db) : ICalendarService
{
    public const int MaxDays = 62;

    public async Task<CalendarResponse> GetAsync(DateOnly from, DateOnly to, bool includePay, CancellationToken cancellationToken)
    {
        if (to < from || to.DayNumber - from.DayNumber >= MaxDays)
        {
            throw new RequestValidationException("to", Texts.Current.RangeTooLong(MaxDays));
        }
        // A ride that starts the evening before can still run into the first day.
        var start = OfficeTime.ToUtc(from.AddDays(-1).ToDateTime(TimeOnly.MinValue))!.Value;
        var end = OfficeTime.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue))!.Value;

        var bookings = await db.Bookings.AsNoTracking()
            .Include(b => b.Client)
            .Include(b => b.Partner)
            .Include(b => b.Assignment).ThenInclude(a => a!.Driver)
            .Include(b => b.Assignment).ThenInclude(a => a!.Vehicle)
            .Where(b => b.PickupAt >= start && b.PickupAt < end && b.Status != BookingStatus.AwaitingPayment)
            .OrderBy(b => b.PickupAt)
            .ToListAsync(cancellationToken);
        var firstMoment = from.ToDateTime(TimeOnly.MinValue);
        var rides = bookings
            .Select(b => new CalendarRide(
                b.Id, b.Reference, b.Status, b.Source,
                OfficeTime.ToLocal(b.PickupAt),
                RideTime.ToLocal(b.PickupAt, b.Pickup),
                b.DurationMinutes,
                b.PickupMeetingPoint?.Name ?? b.Pickup.Address,
                b.DropoffMeetingPoint?.Name ?? b.Dropoff.Address,
                b.ClientId,
                $"{b.Client.FirstName} {b.Client.LastName}".Trim(),
                b.PartnerId,
                b.Partner?.Name,
                b.Passengers,
                b.LargeLuggage,
                b.SmallLuggage,
                b.ChildSeats.Sum(s => s.Count),
                b.VehicleCategory,
                b.DistanceKm,
                b.TotalPrice,
                includePay ? b.DriverPay : null,
                b.AssignmentId,
                b.Assignment?.Status,
                b.Assignment?.DriverId,
                b.Assignment is null ? null : $"{b.Assignment.Driver.FirstName} {b.Assignment.Driver.LastName}",
                b.Assignment?.VehicleId,
                b.Assignment?.Vehicle.PlateNumber,
                b.FlightNumber))
            .Where(r => r.Start.AddMinutes(r.DurationMinutes) > firstMoment)
            .ToList();

        var vehicles = (await db.Vehicles.AsNoTracking().ToListAsync(cancellationToken))
            .OrderBy(v => v.Category).ThenBy(v => v.Code ?? v.PlateNumber)
            .Select(v => new CalendarVehicle(v.Id, v.PlateNumber, v.Code, $"{v.Make} {v.Model}", v.Category, v.Seats, v.IsActive))
            .ToList();
        var drivers = await db.Drivers.AsNoTracking()
            .OrderBy(d => d.LastName).ThenBy(d => d.FirstName)
            .Select(d => new CalendarDriver(d.Id, d.FirstName + " " + d.LastName, d.IsActive, d.IsAvailable))
            .ToListAsync(cancellationToken);
        var absences = await db.AttendanceEntries.AsNoTracking()
            .Where(e => e.Date >= from && e.Date <= to)
            .Select(e => new CalendarAbsence(e.DriverId, e.Date, e.Type.Name, e.Type.Code, e.Type.Color, e.Type.MakesUnavailable,
                e.StartTime, e.EndTime))
            .ToListAsync(cancellationToken);

        return new CalendarResponse(from, to, PricingDefaults.Currency, rides, vehicles, drivers, absences);
    }
}
