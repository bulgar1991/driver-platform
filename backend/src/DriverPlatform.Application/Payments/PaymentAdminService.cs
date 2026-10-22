using System.Globalization;
using System.Text;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Bookings;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Invoices;
using DriverPlatform.Application.Notifications;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using DriverPlatform.Application.Localization;

namespace DriverPlatform.Application.Payments;

public sealed record InvoiceFile(string FileName, byte[] Content);

public interface IPaymentAdminService
{
    Task<IReadOnlyList<PaymentListItem>> ListAsync(PaymentStatus? status, int take, CancellationToken cancellationToken);
    Task<PaymentListItem> RefundAsync(Guid paymentId, RefundRequest request, CancellationToken cancellationToken);
    Task<InvoiceFile> GetInvoicePdfAsync(Guid invoiceId, CancellationToken cancellationToken);
    Task ResendInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken);
    /// <summary>CSV of invoices and credit notes issued in [from, to] (Italian dates), for the accountant.</summary>
    Task<InvoiceFile> ExportInvoicesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

public sealed class PaymentAdminService(
    IAppDbContext db,
    IPaymentGateway gateway,
    IInvoiceService invoices,
    IBookingEmails emails,
    IAuditLogger audit,
    ICurrentUser currentUser,
    TimeProvider clock) : IPaymentAdminService
{
    public async Task<IReadOnlyList<PaymentListItem>> ListAsync(PaymentStatus? status, int take, CancellationToken cancellationToken)
    {
        var query = db.Payments.AsNoTracking().Include(p => p.Booking).ThenInclude(b => b.Client).AsQueryable();
        if (status is not null)
        {
            query = query.Where(p => p.Status == status);
        }
        var payments = await query.OrderByDescending(p => p.CreatedAt).Take(Math.Clamp(take, 1, 500)).ToListAsync(cancellationToken);
        var ids = payments.Select(p => p.Id).ToList();
        var documents = await db.Invoices.AsNoTracking()
            .Where(i => i.PaymentId != null && ids.Contains(i.PaymentId.Value))
            .OrderBy(i => i.IssuedAt)
            .Select(i => new { i.PaymentId, Summary = new InvoiceSummary(i.Id, i.Number, i.Type, i.TotalAmount, i.IssuedAt, i.Status) })
            .ToListAsync(cancellationToken);

        return payments.Select(p => ToItem(p, documents.Where(d => d.PaymentId == p.Id).Select(d => d.Summary).ToList())).ToList();
    }

    public async Task<PaymentListItem> RefundAsync(Guid paymentId, RefundRequest request, CancellationToken cancellationToken)
    {
        var payment = await db.Payments.Include(p => p.Booking).ThenInclude(b => b.Client)
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken)
            ?? throw new NotFoundException(Texts.Current.PaymentNotFound);
        if (payment.Status is not (PaymentStatus.Succeeded or PaymentStatus.PartiallyRefunded) || payment.ProviderTransactionId is null)
        {
            throw new ConflictException(Texts.Current.OnlyPaidRefundable);
        }

        var left = payment.Amount - payment.RefundedAmount;
        var amount = request.Amount ?? left;
        if (amount <= 0 || amount > left)
        {
            throw new RequestValidationException("amount", Texts.Current.RefundRange(Texts.Current.Money(left)));
        }

        var refund = new Refund
        {
            PaymentId = payment.Id,
            Amount = amount,
            Reason = request.Reason.Trim(),
            StaffUserId = currentUser.StaffUserId,
        };
        // The refund id doubles as the idempotency key: a retried request cannot refund twice.
        var result = await gateway.RefundAsync(payment.ProviderTransactionId, amount, refund.Id.ToString(), cancellationToken);
        refund.ProviderRefundId = result.Id;
        refund.ProviderStatus = result.Status;
        db.Refunds.Add(refund);

        payment.RefundedAmount += amount;
        payment.Status = payment.RefundedAmount >= payment.Amount ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;

        if (request.CancelBooking)
        {
            var now = clock.GetUtcNow();
            foreach (var leg in await db.LoadLegsAsync(payment.Booking, cancellationToken))
            {
                leg.Status = BookingStatus.Cancelled;
                leg.CancelledAt = now;
                leg.CancellationReason = refund.Reason;
            }
        }

        audit.Log("payment.refunded", nameof(Payment), payment.Id.ToString(),
            new { payment.Booking.Reference, amount, request.Reason, request.CancelBooking, result.Id });
        await db.SaveChangesAsync(cancellationToken);

        var creditNote = await invoices.IssueCreditNoteAsync(refund, cancellationToken);
        await emails.SendCreditNoteAsync(payment.Booking, creditNote, request.CancelBooking, cancellationToken);

        return (await ListAsync(null, 500, cancellationToken)).First(p => p.Id == payment.Id);
    }

    public async Task<InvoiceFile> GetInvoicePdfAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException(Texts.Current.InvoiceNotFound);
        return new InvoiceFile($"{invoice.Number}.pdf", invoice.Pdf ?? []);
    }

    public async Task ResendInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException(Texts.Current.InvoiceNotFound);
        var outbound = await db.Bookings.Include(b => b.Client).FirstAsync(b => b.Id == invoice.BookingId, cancellationToken);
        if (invoice.Type == InvoiceType.Invoice)
        {
            await emails.SendConfirmationAsync(outbound, await db.LoadLegsAsync(outbound, cancellationToken), invoice, cancellationToken);
        }
        else
        {
            await emails.SendCreditNoteAsync(outbound, invoice, outbound.Status == BookingStatus.Cancelled, cancellationToken);
        }
        audit.Log("invoice.resent", nameof(Invoice), invoice.Id.ToString(), new { invoice.Number });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<InvoiceFile> ExportInvoicesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from)
        {
            throw new RequestValidationException("to", Texts.Current.EndBeforeStart);
        }
        var start = ItalyTime.ToUtc(from.ToDateTime(TimeOnly.MinValue))!.Value;
        var end = ItalyTime.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue))!.Value;
        var rows = await db.Invoices.AsNoTracking()
            .Include(i => i.Booking)
            .Include(i => i.OriginalInvoice)
            .Where(i => i.IssuedAt >= start && i.IssuedAt < end)
            .ToListAsync(cancellationToken);

        // Semicolon-separated with a BOM, so Excel in Italy opens it directly.
        var csv = new StringBuilder("number;type;issued;buyer;email;booking;net;vat;total;currency;original_invoice\n");
        foreach (var i in rows.OrderBy(i => i.Type).ThenBy(i => i.Year).ThenBy(i => i.Sequence))
        {
            var sign = i.Type == InvoiceType.CreditNote ? -1 : 1;
            csv.AppendJoin(';',
                i.Number,
                i.Type,
                ItalyTime.ToLocal(i.IssuedAt).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Csv(i.Buyer.Name),
                Csv(i.Buyer.Email ?? ""),
                i.Booking.Reference,
                Amount(sign * i.NetAmount),
                Amount(sign * i.VatAmount),
                Amount(sign * i.TotalAmount),
                i.Currency,
                i.OriginalInvoice?.Number ?? "").Append('\n');
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return new InvoiceFile($"invoices-{from:yyyy-MM-dd}-{to:yyyy-MM-dd}.csv", bytes);
    }

    private static string Amount(decimal value) => value.ToString("0.00", CultureInfo.GetCultureInfo("it-IT"));

    /// <summary>Quotes the value and neutralises formulas (CSV injection).</summary>
    private static string Csv(string value)
    {
        if (value.Length > 0 && "=+-@".Contains(value[0]))
        {
            value = "'" + value;
        }
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static PaymentListItem ToItem(Payment p, IReadOnlyList<InvoiceSummary> documents) => new(
        p.Id,
        p.Booking.Reference,
        p.Booking.Status,
        $"{p.Booking.Client.FirstName} {p.Booking.Client.LastName}".Trim(),
        p.Booking.Client.Email,
        p.Amount,
        p.RefundedAmount,
        p.Currency,
        p.Status,
        p.Method,
        p.Provider,
        p.CreatedAt,
        p.PaidAt,
        documents);
}
