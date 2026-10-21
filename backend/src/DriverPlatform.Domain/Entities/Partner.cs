using DriverPlatform.Domain.Common;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Domain.Entities;

/// <summary>A company (or Booking) that sends bookings through the API (business plan §6).</summary>
public class Partner : Entity
{
    public string Name { get; set; } = "";
    public PartnerKind Kind { get; set; }
    public string ContactEmail { get; set; } = "";
    /// <summary>Public part of the key ("md_{prefix}_…"), to find the key and tell keys apart in the panel.</summary>
    public string? ApiKeyPrefix { get; set; }
    /// <summary>SHA-256 of the whole key; the key itself is shown once, when created.</summary>
    public string? ApiKeyHash { get; set; }
    public DateTimeOffset? ApiKeyCreatedAt { get; set; }
    public DateTimeOffset? ApiKeyLastUsedAt { get; set; }
    /// <summary>Agreed rate: added to our public price, e.g. −10 for a 10% partner discount.</summary>
    public decimal PriceAdjustmentPercent { get; set; }
    /// <summary>Commission owed to the partner, for the monthly statement.</summary>
    public decimal CommissionPercent { get; set; }
    public bool MonthlyInvoicing { get; set; } = true;
    /// <summary>Whether passengers get our WhatsApp/email messages (driver details, review link).</summary>
    public bool NotifyPassengers { get; set; } = true;
    /// <summary>HTTPS endpoint that receives booking status changes; signed with the partner's webhook secret.</summary>
    public string? WebhookUrl { get; set; }
    /// <summary>Raised to change the webhook secret.</summary>
    public int SecretVersion { get; set; }
    public bool IsActive { get; set; } = true;
}
