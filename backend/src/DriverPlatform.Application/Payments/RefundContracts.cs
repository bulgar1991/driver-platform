using System.ComponentModel.DataAnnotations;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Payments;

public sealed record RefundRequest
{
    /// <summary>Leave empty to refund everything that is left.</summary>
    [Range(0.01, 100_000)]
    public decimal? Amount { get; init; }
    [Required, MaxLength(300)]
    public required string Reason { get; init; }
    /// <summary>Also cancels the ride (both legs of a return trip).</summary>
    public bool CancelBooking { get; init; }
}

public sealed record InvoiceSummary(Guid Id, string Number, InvoiceType Type, decimal TotalAmount, DateTimeOffset IssuedAt, InvoiceStatus Status);

public sealed record PaymentListItem(
    Guid Id,
    string BookingReference,
    BookingStatus BookingStatus,
    string ClientName,
    string? ClientEmail,
    decimal Amount,
    decimal RefundedAmount,
    string Currency,
    PaymentStatus Status,
    PaymentMethod? Method,
    string Provider,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    IReadOnlyList<InvoiceSummary> Invoices);
