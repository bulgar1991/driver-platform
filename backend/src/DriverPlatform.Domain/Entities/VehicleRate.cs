using DriverPlatform.Domain.Common;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Domain.Entities;

/// <summary>
/// A vehicle tariff: price = BaseFee + km × PricePerKm, and what fits in the car. Four come with the
/// platform; more are added in Admin → Pricing. A tariff used by bookings or cars is hidden rather
/// than deleted, so history and invoices keep it.
/// </summary>
public class VehicleRate : Entity
{
    public VehicleCategory Category { get; set; }
    public decimal BaseFee { get; set; }
    public decimal PricePerKm { get; set; }
    public int MaxPassengers { get; set; }
    public int MaxLuggage { get; set; }

    /// <summary>Names set in the panel; empty: the built-in name (Standard, Business, VIP, Bus).</summary>
    public string? NameEn { get; set; }
    public string? NameIt { get; set; }
    public string? NameDe { get; set; }
    public string? NameFr { get; set; }

    /// <summary>Hidden tariffs are not offered for new bookings.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Order on the site and in the panel (cheapest first).</summary>
    public int SortOrder { get; set; }

    /// <summary>The name in a language ("it", "en"…), falling back to English; null when none was set.</summary>
    public string? Name(string language)
    {
        var name = language switch
        {
            "it" => NameIt,
            "de" => NameDe,
            "fr" => NameFr,
            _ => NameEn,
        };
        return string.IsNullOrWhiteSpace(name) ? (string.IsNullOrWhiteSpace(NameEn) ? null : NameEn) : name;
    }
}
