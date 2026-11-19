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

/// <summary>
/// Booking.com Taxi Supplier API. The client id and secret come from user-secrets (development) or
/// deploy/.env (production), never from a committed file.
/// </summary>
public sealed class BookingComOptions
{
    public const string SectionName = "BookingCom";

    public bool Enabled { get; set; }
    /// <summary>OAuth token endpoint (client credentials).</summary>
    public string AuthUrl { get; set; } = "https://auth.dispatchapi.taxi.booking.com/oauth2/token";
    public string ApiUrl { get; set; } = "https://dispatchapi.taxi.booking.com/";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    /// <summary>How often new rides and changes are fetched.</summary>
    public int PollSeconds { get; set; } = 60;
    /// <summary>
    /// Accept every ride (and every amendment) as soon as it arrives. When false, a ride is accepted
    /// once the dispatcher gives it to a driver. Either way, cancelling it in the admin panel rejects it.
    /// </summary>
    public bool AutoAccept { get; set; } = true;
    /// <summary>Rides with a pickup from this long ago on are fetched, so the last ones still get their driver events.</summary>
    public int LookBackHours { get; set; } = 24;
    public int TimeoutSeconds { get; set; } = 20;
}

/// <summary>Two-step sign-in for the panel (authenticator app, or a code by email).</summary>
public sealed class TwoFactorOptions
{
    public const string SectionName = "TwoFactor";

    /// <summary>Everyone must connect an authenticator app before using the panel.</summary>
    public bool Required { get; set; } = true;
    /// <summary>How long a code sent by email works.</summary>
    public int EmailCodeMinutes { get; set; } = 10;
}
