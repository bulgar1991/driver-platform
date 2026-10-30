using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Geo;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using DriverPlatform.Application.Localization;

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
            ?? throw new NotFoundException(Texts.Current.ZoneNotFound);
        Apply(zone, request);

        audit.Log("zone.updated", nameof(Zone), id.ToString(), request);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(zone);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var zone = await db.Zones.FirstOrDefaultAsync(z => z.Id == id, cancellationToken)
            ?? throw new NotFoundException(Texts.Current.ZoneNotFound);
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
                errors.Add("center", Texts.Current.PickCentre);
            }
        }
        else if (request.Polygon.Count < 3)
        {
            errors.Add("polygon", "A polygon needs at least three points.");
        }
        else if (request.Polygon.Any(p => !GeoMath.IsInServiceArea(new Coordinate(p.Latitude, p.Longitude))))
        {
            errors.Add("polygon", Texts.Current.PolygonOutsideServiceArea);
        }

        ValidateRates(request, errors);

        if (request.Type == ZoneType.NoCarAccess && request.MeetingPoints.Count == 0)
        {
            errors.Add("meetingPoints", "A no-car zone needs at least one meeting point reachable by car.");
        }
        for (var i = 0; i < request.MeetingPoints.Count; i++)
        {
            var point = request.MeetingPoints[i];
            if (string.IsNullOrWhiteSpace(point.Name) || string.IsNullOrWhiteSpace(point.Address))
            {
                errors.Add($"meetingPoints[{i}]", Texts.Current.MeetingPointNameAndAddress);
            }
            if (!GeoMath.IsInServiceArea(new Coordinate(point.Latitude, point.Longitude)))
            {
                errors.Add($"meetingPoints[{i}]", Texts.Current.PlaceMeetingPoint);
            }
        }
        if (request.MeetingPoints.GroupBy(m => m.Name.Trim(), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
        {
            errors.Add("meetingPoints", Texts.Current.MeetingPointNamesUnique);
        }
        errors.ThrowIfAny();
    }

    private static void ValidateRates(ZoneRequest request, ValidationErrors errors)
    {
        if (request.Rates.GroupBy(r => r.Category).Any(g => g.Count() > 1))
        {
            errors.Add("rates", Texts.Current.ZoneRateOncePerCategory);
        }
        if (request.Rates.Any(r => r.FixedPrice is < 0 or > 10_000 || r.BaseFee is < 0 or > 1_000 || r.PricePerKm is < 0 or > 100))
        {
            errors.Add("rates", Texts.Current.ZoneRateOutOfRange);
        }
        var usable = request.Pricing switch
        {
            ZonePricing.FixedPrice => request.Rates.Count(r => r.FixedPrice is not null),
            ZonePricing.PerKm => request.Rates.Count(r => r is { BaseFee: not null, PricePerKm: not null }),
            _ => 1,
        };
        if (usable == 0)
        {
            errors.Add("rates", Texts.Current.ZoneRateNeeded);
        }
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
        zone.Pricing = request.Pricing;
        // Only what the chosen pricing uses is kept; a category with nothing set uses the normal rate.
        zone.Rates = request.Pricing switch
        {
            ZonePricing.FixedPrice => request.Rates.Where(r => r.FixedPrice is not null)
                .Select(r => new ZoneRate { Category = r.Category, FixedPrice = r.FixedPrice }).ToList(),
            ZonePricing.PerKm => request.Rates.Where(r => r is { BaseFee: not null, PricePerKm: not null })
                .Select(r => new ZoneRate { Category = r.Category, BaseFee = r.BaseFee, PricePerKm = r.PricePerKm }).ToList(),
            _ => [],
        };
        zone.FixedPriceMaxKm = request.Pricing == ZonePricing.FixedPrice ? request.FixedPriceMaxKm : null;
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
        z.Polygon, z.AccessFee, z.Pricing, z.Rates.OrderBy(r => r.Category).ToList(), z.FixedPriceMaxKm, z.MeetingPoints, z.IsActive);
}
