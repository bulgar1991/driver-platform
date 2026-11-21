using System.ComponentModel.DataAnnotations;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Localization;
using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Dispatch;

public sealed record VehicleExpirationDto(Guid Id, string Title, DateOnly? DueOn, int? DueKm, string? Notes);

public sealed record VehicleExpirationRequest
{
    [Required, MaxLength(80)]
    public required string Title { get; init; }
    public DateOnly? DueOn { get; init; }
    [Range(0, 2_000_000)]
    public int? DueKm { get; init; }
    [MaxLength(500)]
    public string? Notes { get; init; }
}

public sealed record VehicleRefuelDto(
    Guid Id, DateOnly Date, int OdometerKm, decimal Liters, decimal? Cost, string? Station, Guid? DriverId, string? DriverName);

public sealed record VehicleRefuelRequest
{
    public DateOnly Date { get; init; }
    [Range(0, 2_000_000)]
    public int OdometerKm { get; init; }
    [Range(0.1, 2000)]
    public decimal Liters { get; init; }
    [Range(0, 100_000)]
    public decimal? Cost { get; init; }
    [MaxLength(80)]
    public string? Station { get; init; }
    public Guid? DriverId { get; init; }
}

/// <summary>Deadlines of a vehicle, and its refuellings in the period with the consumption they give.</summary>
public sealed record VehicleLogResponse(
    IReadOnlyList<VehicleExpirationDto> Expirations,
    IReadOnlyList<VehicleRefuelDto> Refuels,
    int MileageKm,
    /// <summary>Kilometres between the first and the last reading of the period.</summary>
    int? DistanceKm,
    decimal TotalLiters,
    decimal TotalCost,
    /// <summary>Litres per 100 km; null with fewer than two readings.</summary>
    decimal? AverageConsumption);

public interface IVehicleLogService
{
    Task<VehicleLogResponse> GetAsync(Guid vehicleId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<VehicleExpirationDto> AddExpirationAsync(Guid vehicleId, VehicleExpirationRequest request, CancellationToken cancellationToken);
    Task<VehicleExpirationDto> UpdateExpirationAsync(Guid vehicleId, Guid id, VehicleExpirationRequest request, CancellationToken cancellationToken);
    Task DeleteExpirationAsync(Guid vehicleId, Guid id, CancellationToken cancellationToken);
    Task<VehicleRefuelDto> AddRefuelAsync(Guid vehicleId, VehicleRefuelRequest request, CancellationToken cancellationToken);
    Task DeleteRefuelAsync(Guid vehicleId, Guid id, CancellationToken cancellationToken);
}

public sealed class VehicleLogService(IAppDbContext db, IAuditLogger audit) : IVehicleLogService
{
    /// <summary>A deadline by odometer counts as close this many kilometres before.</summary>
    public const int AlertKm = 1000;

    /// <summary>Deadlines due within <see cref="DocumentRules.AlertDays"/> days or <see cref="AlertKm"/> km (or past).</summary>
    public static int DueSoon(IEnumerable<VehicleExpiration> expirations, int mileageKm, DateOnly today) =>
        expirations.Count(e =>
            (e.DueOn is { } date && date <= today.AddDays(DocumentRules.AlertDays))
            || (e.DueKm is { } km && km <= mileageKm + AlertKm));

    public async Task<VehicleLogResponse> GetAsync(Guid vehicleId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var vehicle = await FindVehicleAsync(vehicleId, cancellationToken);
        var expirations = await db.VehicleExpirations.AsNoTracking().Where(e => e.VehicleId == vehicleId)
            .OrderBy(e => e.DueOn == null).ThenBy(e => e.DueOn).ThenBy(e => e.DueKm)
            .ToListAsync(cancellationToken);
        var refuels = await db.VehicleRefuels.AsNoTracking()
            .Where(r => r.VehicleId == vehicleId && r.Date >= from && r.Date <= to)
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.OdometerKm)
            .Select(r => new VehicleRefuelDto(r.Id, r.Date, r.OdometerKm, r.Liters, r.Cost, r.Station, r.DriverId,
                r.Driver == null ? null : r.Driver.FirstName + " " + r.Driver.LastName))
            .ToListAsync(cancellationToken);

        int? distance = null;
        decimal? consumption = null;
        if (refuels.Count >= 2)
        {
            var byKm = refuels.OrderBy(r => r.OdometerKm).ToList();
            distance = byKm[^1].OdometerKm - byKm[0].OdometerKm;
            // The fuel of the first fill was burnt before the period: only the later fills cover the distance.
            var liters = byKm.Skip(1).Sum(r => r.Liters);
            if (distance > 0)
            {
                consumption = Math.Round(liters / distance.Value * 100, 1);
            }
        }
        return new VehicleLogResponse(
            expirations.Select(ToDto).ToList(), refuels, vehicle.MileageKm, distance,
            refuels.Sum(r => r.Liters), refuels.Sum(r => r.Cost ?? 0), consumption);
    }

    public async Task<VehicleExpirationDto> AddExpirationAsync(Guid vehicleId, VehicleExpirationRequest request, CancellationToken cancellationToken)
    {
        await FindVehicleAsync(vehicleId, cancellationToken);
        var expiration = new VehicleExpiration { VehicleId = vehicleId };
        Apply(expiration, request);
        db.VehicleExpirations.Add(expiration);
        audit.Log("vehicle.expiration_added", nameof(Vehicle), vehicleId.ToString(), request);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(expiration);
    }

    public async Task<VehicleExpirationDto> UpdateExpirationAsync(Guid vehicleId, Guid id, VehicleExpirationRequest request, CancellationToken cancellationToken)
    {
        var expiration = await db.VehicleExpirations.FirstOrDefaultAsync(e => e.Id == id && e.VehicleId == vehicleId, cancellationToken)
            ?? throw new NotFoundException(Texts.Current.RecordNotFound);
        Apply(expiration, request);
        audit.Log("vehicle.expiration_updated", nameof(Vehicle), vehicleId.ToString(), request);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(expiration);
    }

    public async Task DeleteExpirationAsync(Guid vehicleId, Guid id, CancellationToken cancellationToken)
    {
        var expiration = await db.VehicleExpirations.FirstOrDefaultAsync(e => e.Id == id && e.VehicleId == vehicleId, cancellationToken)
            ?? throw new NotFoundException(Texts.Current.RecordNotFound);
        db.VehicleExpirations.Remove(expiration);
        audit.Log("vehicle.expiration_deleted", nameof(Vehicle), vehicleId.ToString(), new { expiration.Title });
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A higher odometer reading also becomes the car's mileage.</summary>
    public async Task<VehicleRefuelDto> AddRefuelAsync(Guid vehicleId, VehicleRefuelRequest request, CancellationToken cancellationToken)
    {
        var vehicle = await FindVehicleAsync(vehicleId, cancellationToken);
        string? driverName = null;
        if (request.DriverId is { } driverId)
        {
            driverName = await db.Drivers.Where(d => d.Id == driverId).Select(d => d.FirstName + " " + d.LastName)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new RequestValidationException("driverId", Texts.Current.DriverNotFound);
        }
        var refuel = new VehicleRefuel
        {
            VehicleId = vehicleId,
            Date = request.Date,
            OdometerKm = request.OdometerKm,
            Liters = request.Liters,
            Cost = request.Cost,
            Station = string.IsNullOrWhiteSpace(request.Station) ? null : request.Station.Trim(),
            DriverId = request.DriverId,
        };
        db.VehicleRefuels.Add(refuel);
        if (request.OdometerKm > vehicle.MileageKm)
        {
            vehicle.MileageKm = request.OdometerKm;
        }
        audit.Log("vehicle.refuel_added", nameof(Vehicle), vehicleId.ToString(), request);
        await db.SaveChangesAsync(cancellationToken);
        return new VehicleRefuelDto(refuel.Id, refuel.Date, refuel.OdometerKm, refuel.Liters, refuel.Cost, refuel.Station,
            refuel.DriverId, driverName);
    }

    public async Task DeleteRefuelAsync(Guid vehicleId, Guid id, CancellationToken cancellationToken)
    {
        var refuel = await db.VehicleRefuels.FirstOrDefaultAsync(r => r.Id == id && r.VehicleId == vehicleId, cancellationToken)
            ?? throw new NotFoundException(Texts.Current.RecordNotFound);
        db.VehicleRefuels.Remove(refuel);
        audit.Log("vehicle.refuel_deleted", nameof(Vehicle), vehicleId.ToString(), new { refuel.Date, refuel.OdometerKm });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Vehicle> FindVehicleAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
        ?? throw new NotFoundException(Texts.Current.VehicleNotFound);

    private static void Apply(VehicleExpiration expiration, VehicleExpirationRequest request)
    {
        if (request.DueOn is null && request.DueKm is null)
        {
            throw new RequestValidationException("dueOn", Texts.Current.GiveDateOrKm);
        }
        expiration.Title = request.Title.Trim();
        expiration.DueOn = request.DueOn;
        expiration.DueKm = request.DueKm;
        expiration.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
    }

    private static VehicleExpirationDto ToDto(VehicleExpiration e) => new(e.Id, e.Title, e.DueOn, e.DueKm, e.Notes);
}
