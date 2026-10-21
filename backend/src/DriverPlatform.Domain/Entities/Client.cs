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

    public ICollection<Booking> Bookings { get; set; } = [];
}
