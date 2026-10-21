using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Geo;
using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Dispatch;

public interface IVehicleAdminService
{
    Task<IReadOnlyList<VehicleDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<VehicleDto> CreateAsync(VehicleRequest request, CancellationToken cancellationToken);
    Task<VehicleDto> UpdateAsync(Guid id, VehicleRequest request, CancellationToken cancellationToken);
}

public sealed class VehicleAdminService(IAppDbContext db, IAuditLogger audit, TimeProvider clock) : IVehicleAdminService
{
    public async Task<IReadOnlyList<VehicleDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var vehicles = await db.Vehicles.AsNoTracking()
            .Where(v => includeInactive || v.IsActive)
            .OrderBy(v => v.PlateNumber)
            .ToListAsync(cancellationToken);
        return vehicles.OrderBy(v => v.Category).ThenBy(v => v.PlateNumber).Select(ToDto).ToList();
    }

    public async Task<VehicleDto> CreateAsync(VehicleRequest request, CancellationToken cancellationToken)
    {
        await EnsureUniquePlateAsync(request.PlateNumber, null, cancellationToken);
        var vehicle = new Vehicle();
        Apply(vehicle, request);
        db.Vehicles.Add(vehicle);

        audit.Log("vehicle.created", nameof(Vehicle), vehicle.Id.ToString(), new { vehicle.PlateNumber, vehicle.Category });
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(vehicle);
    }

    public async Task<VehicleDto> UpdateAsync(Guid id, VehicleRequest request, CancellationToken cancellationToken)
    {
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new NotFoundException("Vehicle not found.");
        await EnsureUniquePlateAsync(request.PlateNumber, id, cancellationToken);
        Apply(vehicle, request);

        audit.Log("vehicle.updated", nameof(Vehicle), id.ToString(), request);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(vehicle);
    }

    private async Task EnsureUniquePlateAsync(string plate, Guid? exceptId, CancellationToken cancellationToken)
    {
        var normalized = NormalizePlate(plate);
        if (await db.Vehicles.AnyAsync(v => v.PlateNumber == normalized && v.Id != exceptId, cancellationToken))
        {
            throw new RequestValidationException("plateNumber", "Another vehicle already has this plate.");
        }
    }

    private static void Apply(Vehicle vehicle, VehicleRequest request)
    {
        vehicle.PlateNumber = NormalizePlate(request.PlateNumber);
        vehicle.Make = request.Make.Trim();
        vehicle.Model = request.Model.Trim();
        vehicle.Color = string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.Trim();
        vehicle.Category = request.Category;
        vehicle.Seats = request.Seats;
        vehicle.LuggageCapacity = request.LuggageCapacity;
        vehicle.ChildSeatsAvailable = request.ChildSeatsAvailable;
        vehicle.InsuranceExpiresOn = request.InsuranceExpiresOn;
        vehicle.InspectionExpiresOn = request.InspectionExpiresOn;
        vehicle.NextServiceOn = request.NextServiceOn;
        vehicle.MileageKm = request.MileageKm;
        vehicle.IsActive = request.IsActive;
        vehicle.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
    }

    private VehicleDto ToDto(Vehicle v) => new(
        v.Id, v.PlateNumber, v.Make, v.Model, v.Color, v.Category, v.Seats, v.LuggageCapacity, v.ChildSeatsAvailable,
        v.InsuranceExpiresOn, v.InspectionExpiresOn, v.NextServiceOn, v.MileageKm, v.IsActive, v.Notes,
        DocumentRules.AlertCount(DocumentRules.Dated(v), DateOnly.FromDateTime(ItalyTime.ToLocal(clock.GetUtcNow()))));

    /// <summary>"ab 123 cd" → "AB123CD".</summary>
    internal static string NormalizePlate(string plate) =>
        new(plate.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
