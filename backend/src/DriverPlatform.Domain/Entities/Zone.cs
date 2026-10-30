using DriverPlatform.Domain.Common;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;

namespace DriverPlatform.Domain.Entities;

/// <summary>A circle or polygon on the map with local rules, configured without code changes.</summary>
public class Zone : Entity
{
    /// <summary>In English; clients see <see cref="NameTranslations"/> in their language.</summary>
    public string Name { get; set; } = "";
    public Translations? NameTranslations { get; set; }
    public ZoneType Type { get; set; }
    public ZoneShape Shape { get; set; }
    public double? CenterLatitude { get; set; }
    public double? CenterLongitude { get; set; }
    public double? RadiusMeters { get; set; }
    public List<PolygonPoint> Polygon { get; set; } = [];
    /// <summary>Fixed fee added to rides entering the zone (e.g. ZTL access).</summary>
    public decimal AccessFee { get; set; }
    public ZonePricing Pricing { get; set; }
    /// <summary>Per vehicle category; a category left out uses the normal rate.</summary>
    public List<ZoneRate> Rates { get; set; } = [];
    /// <summary>A fixed price only applies to rides up to this many km; longer ones use the normal rates. Null: any distance.</summary>
    public decimal? FixedPriceMaxKm { get; set; }
    public List<MeetingPoint> MeetingPoints { get; set; } = [];
    public bool IsActive { get; set; } = true;
}
