using DriverPlatform.Domain.Common;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Domain.Entities;

/// <summary>
/// One payment attempt (a Stripe Checkout session). Card data never reaches us; we only keep the processor's ids.
/// A return trip is paid together with the outbound ride, so the payment belongs to the outbound booking.
/// </summary>
public class Payment : Entity
{
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    /// <summary>Known once paid: the client chooses card or PayPal on the payment page.</summary>
    public PaymentMethod? Method { get; set; }
    public PaymentStatus Status { get; set; }
    public string Provider { get; set; } = "";
    /// <summary>Stripe Checkout session id (cs_…).</summary>
    public string? CheckoutSessionId { get; set; }
    /// <summary>Stripe PaymentIntent id (pi_…), set once paid; refunds go through it.</summary>
    public string? ProviderTransactionId { get; set; }
    public decimal Amount { get; set; }
    public decimal RefundedAmount { get; set; }
    public string Currency { get; set; } = "EUR";
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }

    public ICollection<Refund> Refunds { get; set; } = [];
}
