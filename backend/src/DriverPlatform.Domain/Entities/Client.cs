using DriverPlatform.Domain.Common;

namespace DriverPlatform.Domain.Entities;

public class Client : Entity
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Email { get; set; }
    public string Phone { get; set; } = "";
    /// <summary>ISO 639-1 code, e.g. "en", "it".</summary>
    public string Language { get; set; } = "en";
    public DateTimeOffset? GdprConsentAt { get; set; }
    public bool MarketingConsent { get; set; }
    /// <summary>Staff notes about the client (habits, needs, how to greet them); the driver sees them on every ride. Never shown to the client.</summary>
    public string? NotesForDriver { get; set; }

    // Billing details (Admin → New booking → Customer record); all optional, for invoices to companies.
    public string? Company { get; set; }
    /// <summary>Personal tax code / social id (codice fiscale).</summary>
    public string? TaxCode { get; set; }
    /// <summary>VAT number (P.IVA, UID, USt-IdNr…).</summary>
    public string? VatNumber { get; set; }
    /// <summary>Italian e-invoicing recipient code (codice destinatario, 7 characters).</summary>
    public string? RecipientCode { get; set; }
    public string? PecEmail { get; set; }
    public string? BillingEmail { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    /// <summary>Province or state (e.g. "MI", "VD").</summary>
    public string? Province { get; set; }
    /// <summary>ISO 3166-1 alpha-2, e.g. "CH".</summary>
    public string? Country { get; set; }
    /// <summary>Office notes about the customer (web site, how they pay…); not shown to drivers.</summary>
    public string? OfficeNotes { get; set; }

    public ICollection<Booking> Bookings { get; set; } = [];
}
