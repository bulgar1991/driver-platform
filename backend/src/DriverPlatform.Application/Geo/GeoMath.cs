using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Geo;

public readonly record struct Coordinate(double Latitude, double Longitude);

public static class GeoMath
{
    private const double EarthRadiusMeters = 6_371_000;

    /// <summary>
    /// Rough boxes around the countries served (Maps:ServiceArea, from SERVICE_AREA in deploy/.env):
    /// all of Europe by default, or a list such as "it,fr,ch,de". Bookings outside them are refused.
    /// The boxes also touch neighbours; the precise check is the address search, which only returns
    /// places in the countries served (<see cref="Countries"/>).
    /// </summary>
    public static bool IsInServiceArea(Coordinate c) => IsIn(Boxes, c);

    public static bool IsIn(IEnumerable<(double South, double North, double West, double East)> boxes, Coordinate c) =>
        boxes.Any(box => c.Latitude >= box.South && c.Latitude <= box.North && c.Longitude >= box.West && c.Longitude <= box.East);

    /// <summary>ISO codes (lower case) of the countries served, for the address search.</summary>
    public static IReadOnlyList<string> Countries { get; private set; } = EuropeCountries;

    /// <summary>"europe" (default) or a comma-separated list of ISO country codes.</summary>
    public static void UseServiceArea(string? area)
    {
        var (countries, boxes) = ParseServiceArea(area);
        Countries = countries;
        Boxes = boxes;
    }

    public static (IReadOnlyList<string> Countries, (double South, double North, double West, double East)[] Boxes)
        ParseServiceArea(string? area)
    {
        var value = string.IsNullOrWhiteSpace(area) ? "europe" : area.Trim().ToLowerInvariant();
        if (value == "europe")
        {
            return (EuropeCountries, EuropeBoxes);
        }
        var codes = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
        if (codes.Count == 0 || codes.Any(c => !EuropeCountries.Contains(c)))
        {
            throw new InvalidOperationException(
                $"Maps:ServiceArea must be \"europe\" or European ISO country codes, e.g. \"it,fr,ch,de\" (got \"{area}\").");
        }
        // Countries with their own box use it; any other European country falls back to Europe's boxes.
        var boxes = codes.All(CountryBoxes.ContainsKey) ? codes.Select(c => CountryBoxes[c]).ToArray() : EuropeBoxes;
        return (codes, boxes);
    }

    /// <summary>EU, EEA, the UK, Switzerland, the small states and the Western Balkans.</summary>
    public static readonly IReadOnlyList<string> EuropeCountries =
    [
        "ad", "al", "at", "ba", "be", "bg", "ch", "cy", "cz", "de", "dk", "ee", "es", "fi", "fr", "gb", "gr",
        "hr", "hu", "ie", "is", "it", "li", "lt", "lu", "lv", "mc", "md", "me", "mk", "mt", "nl", "no", "pl",
        "pt", "ro", "rs", "se", "si", "sk", "sm", "va",
    ];

    private static readonly (double South, double North, double West, double East)[] EuropeBoxes =
    [
        (34.7, 71.3, -10.7, 31.7),   // mainland Europe, Ireland, Britain, Scandinavia, Crete, Malta
        (34.5, 35.8, 32.2, 34.7),    // Cyprus
        (63.2, 66.7, -24.6, -13.4),  // Iceland
        (27.6, 29.5, -18.2, -13.3),  // Canary Islands
        (32.3, 33.2, -17.4, -16.2),  // Madeira
        (36.9, 39.8, -31.4, -24.7),  // Azores
    ];

    private static readonly Dictionary<string, (double South, double North, double West, double East)> CountryBoxes = new()
    {
        ["it"] = (35.2, 47.2, 6.5, 18.6),   // with San Marino and Vatican City
        ["fr"] = (41.3, 51.2, -5.3, 9.7),   // with Corsica
        ["ch"] = (45.8, 47.9, 5.9, 10.6),
        ["de"] = (47.2, 55.1, 5.8, 15.1),
    };

    private static (double South, double North, double West, double East)[] Boxes = EuropeBoxes;

    public static bool IsValid(Coordinate c) =>
        c.Latitude is >= -90 and <= 90 && c.Longitude is >= -180 and <= 180;

    public static double DistanceMeters(Coordinate a, Coordinate b)
    {
        var dLat = ToRadians(b.Latitude - a.Latitude);
        var dLon = ToRadians(b.Longitude - a.Longitude);
        var h = Math.Pow(Math.Sin(dLat / 2), 2)
            + Math.Cos(ToRadians(a.Latitude)) * Math.Cos(ToRadians(b.Latitude)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Sqrt(h));
    }

    /// <summary>Ray casting; fine for city-sized polygons.</summary>
    public static bool IsInPolygon(Coordinate point, IReadOnlyList<Coordinate> polygon)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var (a, b) = (polygon[i], polygon[j]);
            if ((a.Latitude > point.Latitude) != (b.Latitude > point.Latitude)
                && point.Longitude < (b.Longitude - a.Longitude) * (point.Latitude - a.Latitude)
                    / (b.Latitude - a.Latitude) + a.Longitude)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    public static bool Contains(Zone zone, Coordinate point) => zone.Shape switch
    {
        ZoneShape.Circle => zone is { CenterLatitude: { } lat, CenterLongitude: { } lng, RadiusMeters: { } r }
            && DistanceMeters(new Coordinate(lat, lng), point) <= r,
        ZoneShape.Polygon => zone.Polygon.Count >= 3
            && IsInPolygon(point, zone.Polygon.Select(p => new Coordinate(p.Latitude, p.Longitude)).ToList()),
        _ => false,
    };

    /// <summary>Used when the map provider is down: straight line × 1.3, at an average 50 km/h.</summary>
    public static RouteEstimate EstimateRoute(Coordinate from, Coordinate to)
    {
        var km = DistanceMeters(from, to) / 1000 * 1.3;
        return new RouteEstimate((decimal)km, (int)Math.Ceiling(km / 50 * 60));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}

public readonly record struct RouteEstimate(decimal DistanceKm, int DurationMinutes);
