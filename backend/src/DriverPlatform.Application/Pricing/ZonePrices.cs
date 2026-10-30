using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Localization;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Pricing;

/// <summary>Zones with their own price (e.g. a fixed airport fare) replace the normal fare of rides that start or end in them.</summary>
public static class ZonePrices
{
    /// <summary>
    /// The pickup's zone first, then the drop-off's. When zones overlap at a point, the smallest one wins:
    /// it is the more specific place (the airport inside the city).
    /// </summary>
    public static Zone? For(IEnumerable<Zone> zones, Coordinate pickup, Coordinate dropoff)
    {
        var priced = zones.Where(z => z.IsActive && z.Pricing != ZonePricing.Standard).ToList();
        Zone? Smallest(Coordinate point) => priced.Where(z => GeoMath.Contains(z, point)).MinBy(Area);
        return Smallest(pickup) ?? Smallest(dropoff);
    }

    /// <summary>The zone's fare for this category, or null when the normal rate applies.</summary>
    public static (decimal Fare, string Label)? Fare(Zone? zone, VehicleCategory category, decimal distanceKm, Texts texts)
    {
        var rate = zone?.Rates.FirstOrDefault(r => r.Category == category);
        if (zone is null || rate is null)
        {
            return null;
        }
        var name = zone.DisplayName(texts);
        return zone.Pricing switch
        {
            ZonePricing.FixedPrice when rate.FixedPrice is { } price && (zone.FixedPriceMaxKm is not { } max || distanceKm <= max)
                => (PriceCalculator.Money(price), texts.ZoneFixedFareLine(name, category)),
            ZonePricing.PerKm when rate is { BaseFee: { } baseFee, PricePerKm: { } perKm }
                => (PriceCalculator.Money(baseFee + distanceKm * perKm), texts.ZoneFareLine(name, category, distanceKm)),
            _ => null,
        };
    }

    /// <summary>Only to compare zones: square degrees are fine.</summary>
    private static double Area(Zone zone) => zone.Shape switch
    {
        ZoneShape.Circle => Math.PI * Math.Pow(zone.RadiusMeters ?? 0, 2) / 1e10,
        _ => Math.Abs(zone.Polygon.Select((p, i) =>
        {
            var next = zone.Polygon[(i + 1) % zone.Polygon.Count];
            return p.Longitude * next.Latitude - next.Longitude * p.Latitude;
        }).Sum()) / 2 * 111.32 * 111.32 * 1e6 / 1e10,
    };
}
