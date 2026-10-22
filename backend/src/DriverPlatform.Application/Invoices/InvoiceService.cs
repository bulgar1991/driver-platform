using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Bookings;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Localization;
using DriverPlatform.Application.Pricing;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DriverPlatform.Application.Invoices;

public interface IInvoiceService
{
    /// <summary>Issues the invoice for a paid booking (both legs of a return trip). Returns the existing one if already issued.</summary>
    Task<Invoice> IssueInvoiceAsync(Payment payment, CancellationToken cancellationToken);

    Task<Invoice> IssueCreditNoteAsync(Refund refund, CancellationToken cancellationToken);
}

public sealed class InvoiceService(
    IAppDbContext db,
    IInvoiceRenderer renderer,
    IOptions<CompanyOptions> company,
    TimeProvider clock) : IInvoiceService
{
    // Numbers must have no gaps or duplicates; the unique index (type, year, sequence) is the safety net.
    private static readonly SemaphoreSlim NumberingGate = new(1, 1);

    public async Task<Invoice> IssueInvoiceAsync(Payment payment, CancellationToken cancellationToken)
    {
        var existing = await db.Invoices.FirstOrDefaultAsync(
            i => i.PaymentId == payment.Id && i.Type == InvoiceType.Invoice, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var outbound = await db.Bookings.Include(b => b.Client).FirstAsync(b => b.Id == payment.BookingId, cancellationToken);
        var legs = await db.LoadLegsAsync(outbound, cancellationToken);
        var texts = Texts.For(outbound.Client.Language);
        var lines = legs.SelectMany(leg => InvoiceLines(leg, texts)).ToList();

        return await IssueAsync(InvoiceType.Invoice, outbound, lines, payment.Id, null, null, cancellationToken);
    }

    public async Task<Invoice> IssueCreditNoteAsync(Refund refund, CancellationToken cancellationToken)
    {
        var payment = await db.Payments.FirstAsync(p => p.Id == refund.PaymentId, cancellationToken);
        var original = await db.Invoices.FirstOrDefaultAsync(
                i => i.PaymentId == payment.Id && i.Type == InvoiceType.Invoice, cancellationToken)
            ?? await IssueInvoiceAsync(payment, cancellationToken);
        var outbound = await db.Bookings.Include(b => b.Client).FirstAsync(b => b.Id == payment.BookingId, cancellationToken);

        var line = Line(
            Texts.For(outbound.Client.Language).RefundForInvoice(original.Number) + (string.IsNullOrWhiteSpace(refund.Reason) ? "" : $": {refund.Reason}"),
            refund.Amount,
            outbound.VatRate);
        return await IssueAsync(InvoiceType.CreditNote, outbound, [line], payment.Id, refund.Id, original, cancellationToken);
    }

    private async Task<Invoice> IssueAsync(
        InvoiceType type,
        Booking outbound,
        List<InvoiceLine> lines,
        Guid paymentId,
        Guid? refundId,
        Invoice? original,
        CancellationToken cancellationToken)
    {
        await NumberingGate.WaitAsync(cancellationToken);
        try
        {
            var now = clock.GetUtcNow();
            var year = ItalyTime.ToLocal(now).Year;
            var last = await db.Invoices
                .Where(i => i.Type == type && i.Year == year)
                .MaxAsync(i => (int?)i.Sequence, cancellationToken) ?? 0;
            var prefix = type == InvoiceType.Invoice ? company.Value.InvoicePrefix : company.Value.CreditNotePrefix;

            var invoice = new Invoice
            {
                Type = type,
                Year = year,
                Sequence = last + 1,
                Number = $"{prefix}-{year}-{last + 1:000000}",
                Status = InvoiceStatus.Issued,
                BookingId = outbound.Id,
                PaymentId = paymentId,
                RefundId = refundId,
                OriginalInvoiceId = original?.Id,
                Seller = Seller(),
                Buyer = new InvoiceParty
                {
                    Name = $"{outbound.Client.FirstName} {outbound.Client.LastName}".Trim(),
                    Email = outbound.Client.Email,
                    Phone = outbound.Client.Phone,
                },
                Lines = lines,
                NetAmount = lines.Sum(l => l.NetAmount),
                VatAmount = lines.Sum(l => l.VatAmount),
                TotalAmount = lines.Sum(l => l.TotalAmount),
                Currency = outbound.Currency,
                IssuedAt = now,
            };
            invoice.Pdf = renderer.RenderPdf(invoice, outbound.Reference, original?.Number);

            db.Invoices.Add(invoice);
            await db.SaveChangesAsync(cancellationToken);
            return invoice;
        }
        finally
        {
            NumberingGate.Release();
        }
    }

    /// <summary>
    /// The ride (fare, surcharges and discount) as one line, then each zone fee and extra, in the
    /// client's language like the price lines (the layout of the PDF is Italian and English).
    /// </summary>
    private static IEnumerable<InvoiceLine> InvoiceLines(Booking booking, Texts t)
    {
        var ride = booking.PriceLines
            .Where(l => l.Kind is PriceLineKind.Fare or PriceLineKind.Surcharge or PriceLineKind.Discount)
            .Sum(l => l.Amount);
        yield return Line(
            $"{booking.Describe(t)} ({t.FareLine(booking.VehicleCategory, booking.DistanceKm)})",
            ride,
            booking.VatRate);

        foreach (var line in booking.PriceLines.Where(l => l.Kind is PriceLineKind.ZoneFee or PriceLineKind.Extra))
        {
            yield return Line($"{line.Label} – {booking.Reference}", line.Amount, booking.VatRate, line.Quantity);
        }
    }

    private static InvoiceLine Line(string description, decimal total, decimal vatRate, int quantity = 1)
    {
        var net = PriceCalculator.Money(total / (1 + vatRate / 100));
        return new InvoiceLine
        {
            Description = description,
            Quantity = quantity,
            VatRatePercent = vatRate,
            NetAmount = net,
            VatAmount = total - net,
            TotalAmount = total,
        };
    }

    private InvoiceParty Seller()
    {
        var c = company.Value;
        return new InvoiceParty
        {
            Name = c.Name,
            Address = Blank(c.Address),
            VatNumber = Blank(c.VatNumber),
            TaxCode = Blank(c.TaxCode),
            Email = Blank(c.Email),
            Phone = Blank(c.Phone),
        };
    }

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
