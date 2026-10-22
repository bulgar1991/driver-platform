using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Geo;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Admin;

public interface IZoneAdminService
{
    Task<IReadOnlyList<ZoneAdminDto>> ListAsync(CancellationToken cancellationToken);
    Task<ZoneAdminDto> CreateAsync(ZoneRequest request, CancellationToken cancellationToken);
    Task<ZoneAdminDto> UpdateAsync(Guid id, ZoneRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Circles and polygons on the map with local rules (business plan §2, §5).</summary>
public sealed class ZoneAdminService(IAppDbContext db, IAuditLogger audit) : IZoneAdminService
{
    public async Task<IReadOnlyList<ZoneAdminDto>> ListAsync(CancellationToken cancellationToken)
    {
        var zones = await db.Zones.AsNoTracking().OrderBy(z => z.Name).ToListAsync(cancellationToken);
        return zones.Select(ToDto).ToList();
    }

    public async Task<ZoneAdminDto> CreateAsync(ZoneRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        var zone = new Zone();
        Apply(zone, request);
        db.Zones.Add(zone);

        audit.Log("zone.created", nameof(Zone), zone.Id.ToString(), new { request.Name, request.Type });
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(zone);
    }

    public async Task<ZoneAdminDto> UpdateAsync(Guid id, ZoneRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        var zone = await db.Zones.FirstOrDefaultAsync(z => z.Id == id, cancellationToken)
            ?? throw new NotFoundException("Zone not found.");
        Apply(zone, request);

        audit.Log("zone.updated", nameof(Zone), id.ToString(), request);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(zone);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var zone = await db.Zones.FirstOrDefaultAsync(z => z.Id == id, cancellationToken)
            ?? throw new NotFoundException("Zone not found.");
        db.Zones.Remove(zone);

        audit.Log("zone.deleted", nameof(Zone), id.ToString(), new { zone.Name, zone.Type });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(ZoneRequest request)
    {
        var errors = new ValidationErrors();
        if (request.Shape == ZoneShape.Circle)
        {
            if (request is not { CenterLatitude: { } lat, CenterLongitude: { } lng, RadiusMeters: not null }
                || !GeoMath.IsInServiceArea(new Coordinate(lat, lng)))
            {
                errors.Add("center", "Pick the centre on the map and set a radius.");
            }
        }
        else if (request.Polygon.Count < 3)
        {
            errors.Add("polygon", "A polygon needs at least three points.");
        }
        else if (request.Polygon.Any(p => !GeoMath.IsInServiceArea(new Coordinate(p.Latitude, p.Longitude))))
        {
            errors.Add("polygon", "All points must be in the service area (Italy, France, Germany, Switzerland).");
        }

        if (request.Type == ZoneType.NoCarAccess && request.MeetingPoints.Count == 0)
        {
            errors.Add("meetingPoints", "A no-car zone needs at least one meeting point reachable by car.");
        }
        for (var i = 0; i < request.MeetingPoints.Count; i++)
        {
            var point = request.MeetingPoints[i];
            if (string.IsNullOrWhiteSpace(point.Name) || string.IsNullOrWhiteSpace(point.Address))
            {
                errors.Add($"meetingPoints[{i}]", "Name and address are required.");
            }
            if (!GeoMath.IsInServiceArea(new Coordinate(point.Latitude, point.Longitude)))
            {
                errors.Add($"meetingPoints[{i}]", "Pick the meeting point on the map.");
            }
        }
        if (request.MeetingPoints.GroupBy(m => m.Name.Trim(), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
        {
            errors.Add("meetingPoints", "Meeting point names must be unique within the zone.");
        }
        errors.ThrowIfAny();
    }

    private static void Apply(Zone zone, ZoneRequest request)
    {
        var circle = request.Shape == ZoneShape.Circle;
        zone.Name = request.Name.Trim();
        zone.NameTranslations = request.NameTranslations.Cleaned();
        zone.Type = request.Type;
        zone.Shape = request.Shape;
        zone.CenterLatitude = circle ? request.CenterLatitude : null;
        zone.CenterLongitude = circle ? request.CenterLongitude : null;
        zone.RadiusMeters = circle ? request.RadiusMeters : null;
        zone.Polygon = circle ? [] : request.Polygon.Select(p => new PolygonPoint { Latitude = p.Latitude, Longitude = p.Longitude }).ToList();
        zone.AccessFee = request.AccessFee;
        zone.IsActive = request.IsActive;
        zone.MeetingPoints = request.MeetingPoints.Select(m => new MeetingPoint
        {
            Name = m.Name.Trim(),
            NameTranslations = m.NameTranslations.Cleaned(),
            Address = m.Address.Trim(),
            Latitude = m.Latitude,
            Longitude = m.Longitude,
            ClientInstructions = string.IsNullOrWhiteSpace(m.ClientInstructions) ? null : m.ClientInstructions.Trim(),
            ClientInstructionsTranslations = m.ClientInstructionsTranslations.Cleaned(),
            DriverInstructions = string.IsNullOrWhiteSpace(m.DriverInstructions) ? null : m.DriverInstructions.Trim(),
        }).ToList();
    }

    private static ZoneAdminDto ToDto(Zone z) => new(
        z.Id, z.Name, z.NameTranslations ?? new Translations(), z.Type, z.Shape, z.CenterLatitude, z.CenterLongitude, z.RadiusMeters,
        z.Polygon, z.AccessFee, z.MeetingPoints, z.IsActive);
}
