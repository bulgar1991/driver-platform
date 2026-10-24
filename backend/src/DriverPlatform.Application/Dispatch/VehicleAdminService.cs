using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Geo;
using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using DriverPlatform.Application.Localization;

namespace DriverPlatform.Application.Dispatch;

public interface IVehicleAdminService
{
    Task<IReadOnlyList<VehicleDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<VehicleDto> CreateAsync(VehicleRequest request, CancellationToken cancellationToken);
    Task<VehicleDto> UpdateAsync(Guid id, VehicleRequest request, CancellationToken cancellationToken);
    Task<VehicleDto> SetPhotoAsync(Guid id, byte[] data, CancellationToken cancellationToken);
    Task<VehicleDto> RemovePhotoAsync(Guid id, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
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
        var vehicle = await FindAsync(id, cancellationToken);
        await EnsureUniquePlateAsync(request.PlateNumber, id, cancellationToken);
        Apply(vehicle, request);

        audit.Log("vehicle.updated", nameof(Vehicle), id.ToString(), request);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(vehicle);
    }

    public async Task<VehicleDto> SetPhotoAsync(Guid id, byte[] data, CancellationToken cancellationToken)
    {
        var vehicle = await FindAsync(id, cancellationToken);
        Photos.Remove(db, vehicle.PhotoId);
        vehicle.PhotoId = Photos.Add(db, data).Id;

        audit.Log("vehicle.photo_set", nameof(Vehicle), id.ToString(), new { data.Length });
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(vehicle);
    }

    public async Task<VehicleDto> RemovePhotoAsync(Guid id, CancellationToken cancellationToken)
    {
        var vehicle = await FindAsync(id, cancellationToken);
        Photos.Remove(db, vehicle.PhotoId);
        vehicle.PhotoId = null;

        audit.Log("vehicle.photo_removed", nameof(Vehicle), id.ToString());
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(vehicle);
    }

    /// <summary>Only a vehicle never put on a shift: past shifts must keep pointing at it, so those are deactivated.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var vehicle = await FindAsync(id, cancellationToken);
        if (await db.Assignments.AnyAsync(a => a.VehicleId == id, cancellationToken))
        {
            throw new ConflictException(Texts.Current.VehicleHasShifts);
        }
        Photos.Remove(db, vehicle.PhotoId);
        db.Vehicles.Remove(vehicle);

        audit.Log("vehicle.deleted", nameof(Vehicle), id.ToString(), new { vehicle.PlateNumber });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Vehicle> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
        ?? throw new NotFoundException(Texts.Current.VehicleNotFound);

    private async Task EnsureUniquePlateAsync(string plate, Guid? exceptId, CancellationToken cancellationToken)
    {
        var normalized = NormalizePlate(plate);
        if (await db.Vehicles.AnyAsync(v => v.PlateNumber == normalized && v.Id != exceptId, cancellationToken))
        {
            throw new RequestValidationException("plateNumber", Texts.Current.PlateTaken);
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
        v.InsuranceExpiresOn, v.InspectionExpiresOn, v.NextServiceOn, v.MileageKm, v.IsActive, v.Notes, v.PhotoId,
        DocumentRules.AlertCount(DocumentRules.Dated(v), DateOnly.FromDateTime(ItalyTime.ToLocal(clock.GetUtcNow()))));

    /// <summary>"ab 123 cd" → "AB123CD".</summary>
    internal static string NormalizePlate(string plate) =>
        new(plate.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
