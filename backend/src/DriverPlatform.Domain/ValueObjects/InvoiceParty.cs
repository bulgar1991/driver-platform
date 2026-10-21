namespace DriverPlatform.Domain.ValueObjects;

/// <summary>Seller or buyer as printed on the invoice, frozen at issue time.</summary>
public sealed class InvoiceParty
{
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string? VatNumber { get; set; }
    public string? TaxCode { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}
