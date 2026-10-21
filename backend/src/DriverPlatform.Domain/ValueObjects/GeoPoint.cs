namespace DriverPlatform.Domain.ValueObjects;

/// <summary>An address with coordinates, as picked on the map.</summary>
public sealed class GeoPoint
{
    public string Address { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
