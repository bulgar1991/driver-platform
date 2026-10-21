using DriverPlatform.Domain.Common;

namespace DriverPlatform.Domain.Entities;

/// <summary>A full or partial refund of a payment; each one gets a credit note.</summary>
public class Refund : Entity
{
    public Guid PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";
    /// <summary>Stripe refund id (re_…).</summary>
    public string? ProviderRefundId { get; set; }
    /// <summary>The processor's status, e.g. "succeeded" or "pending" (PayPal can take a while).</summary>
    public string ProviderStatus { get; set; } = "";
    public Guid? StaffUserId { get; set; }
}
