using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Geo;

public readonly record struct Coordinate(double Latitude, double Longitude);

public static class GeoMath
{
    private const double EarthRadiusMeters = 6_371_000;

    /// <summary>Rough box around Italy, San Marino and Vatican City; bookings outside it are refused.</summary>
    public static bool IsInItaly(Coordinate c) =>
        c.Latitude is >= 35.2 and <= 47.2 && c.Longitude is >= 6.5 and <= 18.6;

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
