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
    public List<MeetingPoint> MeetingPoints { get; set; } = [];
    public bool IsActive { get; set; } = true;
}
