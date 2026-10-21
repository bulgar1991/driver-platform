namespace DriverPlatform.Application.Common;

public sealed class PaymentsOptions
{
    public const string SectionName = "Payments";

    /// <summary>"Stripe", or "Fake" for development without Stripe keys.</summary>
    public string Provider { get; set; } = "Fake";
    /// <summary>Where Stripe sends the client back, e.g. "https://magnusdrive.it".</summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:4200";
    /// <summary>Stripe's minimum is 30 minutes.</summary>
    public int CheckoutExpiryMinutes { get; set; } = 30;
}

/// <summary>The seller printed on invoices. To fill in with the company's registration data.</summary>
public sealed class CompanyOptions
{
    public const string SectionName = "Company";

    public string Name { get; set; } = "Magnus Drive";
    public string Address { get; set; } = "";
    public string VatNumber { get; set; } = "";
    public string TaxCode { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string InvoicePrefix { get; set; } = "MD";
    public string CreditNotePrefix { get; set; } = "NC";
}

public sealed class LinksOptions
{
    public const string SectionName = "Links";

    /// <summary>Signs the client's "my booking" links; at least 32 characters. Changing it invalidates sent links.</summary>
    public string SigningKey { get; set; } = "";
}
