using System.Collections.Concurrent;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.ValueObjects;
using GeoTimeZone;

namespace DriverPlatform.Application.Geo;

/// <summary>
/// Ride times are entered and shown in the local time where the ride starts (London rides in UK
/// time, Athens rides in Greek time), whatever the client's or the office's time zone. The zone
/// comes from the pickup's coordinates (offline lookup, no web service).
/// </summary>
public static class RideTime
{
    private static readonly ConcurrentDictionary<string, TimeZoneInfo> Zones = new();

    /// <summary>The time zone at a point; the office's zone when the point has none (open sea).</summary>
    public static TimeZoneInfo ZoneAt(double latitude, double longitude)
    {
        var id = TimeZoneLookup.GetTimeZone(latitude, longitude).Result;
        return Zones.GetOrAdd(id, key =>
            TimeZoneInfo.TryFindSystemTimeZoneById(key, out var zone) ? zone : OfficeTime.Zone);
    }

    public static TimeZoneInfo ZoneAt(Coordinate point) => ZoneAt(point.Latitude, point.Longitude);

    public static TimeZoneInfo ZoneAt(GeoPoint point) => ZoneAt(point.Latitude, point.Longitude);

    /// <summary>
    /// A shift's local times: those of its first ride's pickup (a driver works in one region);
    /// the office's zone for a shift without rides.
    /// </summary>
    public static TimeZoneInfo ZoneOf(IEnumerable<Booking> rides) =>
        rides.OrderBy(b => b.PickupAt).FirstOrDefault() is { } first ? ZoneAt(first.Pickup) : OfficeTime.Zone;

    /// <summary>A pickup time as on the client's watch at the pickup.</summary>
    public static DateTime ToLocal(DateTimeOffset utc, GeoPoint pickup) => LocalTime.ToLocal(utc, ZoneAt(pickup));

    /// <summary>Null when the local time does not exist there (the hour skipped when clocks go forward).</summary>
    public static DateTimeOffset? ToUtc(DateTime local, Coordinate pickup) => LocalTime.ToUtc(local, ZoneAt(pickup));
}
