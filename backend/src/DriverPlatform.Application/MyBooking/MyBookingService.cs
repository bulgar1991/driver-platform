using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Bookings;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Payments;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.MyBooking;

public sealed record MyPayment(PaymentStatus Status, PaymentMethod? Method, decimal Amount, decimal RefundedAmount, DateTimeOffset? PaidAt);

public sealed record MyInvoice(string Number, InvoiceType Type, decimal TotalAmount, DateTimeOffset IssuedAt);

public sealed record MyBookingResponse(
    string Reference,
    BookingStatus Status,
    VehicleCategory VehicleCategory,
    string ClientName,
    IReadOnlyList<BookingLegSummary> Legs,
    decimal Total,
    decimal VatRatePercent,
    string Currency,
    bool CanPay,
    MyPayment? Payment,
    IReadOnlyList<MyInvoice> Invoices);

public interface IMyBookingService
{
    Task<MyBookingResponse> GetAsync(string reference, string token, CancellationToken cancellationToken);
    Task<string> StartCheckoutAsync(string reference, string token, CancellationToken cancellationToken);
    Task<InvoiceFile> GetInvoicePdfAsync(string reference, string token, string number, CancellationToken cancellationToken);
}

/// <summary>The client's own page, opened from the signed link in the confirmation (no account needed).</summary>
public sealed class MyBookingService(IAppDbContext db, IBookingLinks links, IPaymentService payments) : IMyBookingService
{
    public async Task<MyBookingResponse> GetAsync(string reference, string token, CancellationToken cancellationToken)
    {
        var outbound = await FindAsync(reference, token, cancellationToken);
        var legs = await db.LoadLegsAsync(outbound, cancellationToken);
        var payment = await db.Payments.AsNoTracking()
            .Where(p => p.BookingId == outbound.Id)
            .OrderByDescending(p => p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.Refunded || p.Status == PaymentStatus.PartiallyRefunded)
            .ThenByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var invoices = await db.Invoices.AsNoTracking()
            .Where(i => i.BookingId == outbound.Id)
            .OrderBy(i => i.IssuedAt)
            .Select(i => new MyInvoice(i.Number, i.Type, i.TotalAmount, i.IssuedAt))
            .ToListAsync(cancellationToken);

        return new MyBookingResponse(
            outbound.Reference,
            outbound.Status,
            outbound.VehicleCategory,
            $"{outbound.Client.FirstName} {outbound.Client.LastName}".Trim(),
            legs.Select(b => b.ToLegSummary()).ToList(),
            legs.Sum(b => b.TotalPrice),
            outbound.VatRate,
            outbound.Currency,
            outbound.Status == BookingStatus.AwaitingPayment,
            payment is null ? null : new MyPayment(payment.Status, payment.Method, payment.Amount, payment.RefundedAmount, payment.PaidAt),
            invoices);
    }

    public async Task<string> StartCheckoutAsync(string reference, string token, CancellationToken cancellationToken)
    {
        var outbound = await FindAsync(reference, token, cancellationToken);
        return await payments.StartCheckoutAsync(outbound, cancellationToken);
    }

    public async Task<InvoiceFile> GetInvoicePdfAsync(string reference, string token, string number, CancellationToken cancellationToken)
    {
        var outbound = await FindAsync(reference, token, cancellationToken);
        var invoice = await db.Invoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.BookingId == outbound.Id && i.Number == number, cancellationToken)
            ?? throw new NotFoundException("Invoice not found.");
        return new InvoiceFile($"{invoice.Number}.pdf", invoice.Pdf ?? []);
    }

    /// <summary>A wrong reference and a wrong token give the same answer, so references cannot be probed.</summary>
    private async Task<Booking> FindAsync(string reference, string token, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.Include(b => b.Client)
            .FirstOrDefaultAsync(b => b.Reference == reference.ToUpperInvariant() && b.ReturnOfBookingId == null, cancellationToken);
        if (booking is null || !links.IsValid(booking, token))
        {
            throw new NotFoundException("Booking not found.");
        }
        return booking;
    }
}
