using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Domain.ValueObjects;

/// <summary>A zone's price for one vehicle category; which fields count depends on the zone's <see cref="ZonePricing"/>.</summary>
public sealed class ZoneRate
{
    public VehicleCategory Category { get; set; }
    public decimal? FixedPrice { get; set; }
    public decimal? BaseFee { get; set; }
    public decimal? PricePerKm { get; set; }
}
