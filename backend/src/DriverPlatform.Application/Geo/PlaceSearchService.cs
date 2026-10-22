using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Localization;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Geo;

public enum PlaceKind
{
    /// <summary>A predefined meeting point: airport, station, port, Venice car terminal.</summary>
    MeetingPoint,
    Address,
}

public sealed record PlaceDto(string Label, string Address, double Latitude, double Longitude, PlaceKind Kind);

public interface IPlaceSearchService
{
    Task<IReadOnlyList<PlaceDto>> SearchAsync(string query, string language, CancellationToken cancellationToken);
    Task<PlaceDto?> ReverseAsync(double latitude, double longitude, string language, CancellationToken cancellationToken);
}

/// <summary>Predefined meeting points first, then addresses from the map provider.</summary>
public sealed class PlaceSearchService(IAppDbContext db, IGeocodingService geocoding) : IPlaceSearchService
{
    private const int MaxPredefined = 3;

    public async Task<IReadOnlyList<PlaceDto>> SearchAsync(string query, string language, CancellationToken cancellationToken)
    {
        query = query.Trim();
        var texts = Texts.For(language);
        var zones = await db.Zones.AsNoTracking().Where(z => z.IsActive).ToListAsync(cancellationToken);
        var predefined = zones
            .Where(z => z.Type != ZoneType.LimitedTraffic)
            .SelectMany(z => z.MeetingPoints.Select(m => (Zone: z, Point: m)))
            // "Flughafen" finds the airports on the German site, "Fiumicino" on every site.
            .Where(x => Matches(x.Point.Name, query) || Matches(x.Zone.Name, query) || Matches(x.Point.Address, query)
                || Matches(x.Point.DisplayName(texts), query) || Matches(x.Zone.DisplayName(texts), query))
            .Take(MaxPredefined)
            .Select(x => new PlaceDto($"{x.Point.DisplayName(texts)} · {x.Zone.DisplayName(texts)}", x.Point.Address, x.Point.Latitude, x.Point.Longitude, PlaceKind.MeetingPoint))
            .ToList();

        var addresses = await geocoding.SearchAsync(query, language, cancellationToken);
        return predefined
            .Concat(addresses.Select(a => new PlaceDto(a.Label, a.Address, a.Latitude, a.Longitude, PlaceKind.Address)))
            .ToList();
    }

    public async Task<PlaceDto?> ReverseAsync(double latitude, double longitude, string language, CancellationToken cancellationToken)
    {
        var address = await geocoding.ReverseAsync(latitude, longitude, language, cancellationToken);
        return address is null ? null : new PlaceDto(address.Label, address.Address, latitude, longitude, PlaceKind.Address);
    }

    private static bool Matches(string text, string query) =>
        text.Contains(query, StringComparison.OrdinalIgnoreCase);
}
