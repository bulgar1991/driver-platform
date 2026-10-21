using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Domain.ValueObjects;

public sealed class PriceLine
{
    public PriceLineKind Kind { get; set; }
    public string Label { get; set; } = "";
    public int Quantity { get; set; } = 1;
    /// <summary>Line total in EUR, VAT included; negative for discounts.</summary>
    public decimal Amount { get; set; }
}
