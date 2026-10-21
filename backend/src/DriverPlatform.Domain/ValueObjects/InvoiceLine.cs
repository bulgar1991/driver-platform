namespace DriverPlatform.Domain.ValueObjects;

/// <summary>An invoice line. Prices are VAT-inclusive; net and VAT are derived and stored as issued.</summary>
public sealed class InvoiceLine
{
    public string Description { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public decimal VatRatePercent { get; set; }
    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
}
