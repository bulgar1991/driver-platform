using DriverPlatform.Domain.Common;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;

namespace DriverPlatform.Domain.Entities;

/// <summary>An invoice or credit note. Numbers run per type and year without gaps, e.g. "MD-2026-000001".</summary>
public class Invoice : Entity
{
    public string Number { get; set; } = "";
    public InvoiceType Type { get; set; }
    public int Year { get; set; }
    public int Sequence { get; set; }
    public InvoiceStatus Status { get; set; }
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public Guid? PaymentId { get; set; }
    public Payment? Payment { get; set; }
    public Guid? RefundId { get; set; }
    public Refund? Refund { get; set; }
    /// <summary>For a credit note: the invoice it reverses.</summary>
    public Guid? OriginalInvoiceId { get; set; }
    public Invoice? OriginalInvoice { get; set; }
    public InvoiceParty Seller { get; set; } = new();
    public InvoiceParty Buyer { get; set; } = new();
    public List<InvoiceLine> Lines { get; set; } = [];
    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "EUR";
    /// <summary>The PDF as sent, kept so the document never changes.</summary>
    public byte[]? Pdf { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset? EmailSentAt { get; set; }
}
