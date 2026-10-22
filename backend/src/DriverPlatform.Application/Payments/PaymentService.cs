using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Bookings;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Invoices;
using DriverPlatform.Application.Notifications;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DriverPlatform.Application.Localization;

namespace DriverPlatform.Application.Payments;

public interface IPaymentService
{
    /// <summary>Opens a payment page for the booking (and its return leg) and returns its URL.</summary>
    Task<string> StartCheckoutAsync(Booking outbound, CancellationToken cancellationToken);

    /// <summary>Stripe webhook: verifies the signature, then handles the event.</summary>
    Task HandleWebhookAsync(string payload, string signature, CancellationToken cancellationToken);

    /// <summary>Safe to call more than once for the same event (Stripe retries webhooks).</summary>
    Task HandleEventAsync(PaymentEvent paymentEvent, CancellationToken cancellationToken);
}

/// <summary>
/// Booking → Checkout → webhook → booking "New" + invoice + email (business plan §5).
/// The amount always comes from the booking stored in the database.
/// </summary>
public sealed class PaymentService(
    IAppDbContext db,
    IPaymentGateway gateway,
    IInvoiceService invoices,
    IBookingEmails emails,
    IBookingLinks links,
    IAuditLogger audit,
    IOptions<PaymentsOptions> options,
    TimeProvider clock,
    ILogger<PaymentService> logger) : IPaymentService
{
    public async Task<string> StartCheckoutAsync(Booking outbound, CancellationToken cancellationToken)
    {
        if (outbound.Status != BookingStatus.AwaitingPayment)
        {
            throw new ConflictException(outbound.Status == BookingStatus.Cancelled
                ? Texts.Current.BookingWasCancelled
                : Texts.Current.BookingAlreadyPaid);
        }
        if (outbound.ReturnOfBookingId is not null)
        {
            throw new RequestValidationException("reference", Texts.Current.PayTheOutbound);
        }

        // Loaded here if needed: the return link to the client's page is in the client's language.
        var client = outbound.Client ??= await db.Clients.FirstAsync(c => c.Id == outbound.ClientId, cancellationToken);
        var legs = await db.LoadLegsAsync(outbound, cancellationToken);
        var payment = new Payment
        {
            BookingId = outbound.Id,
            Status = PaymentStatus.Pending,
            Provider = gateway.Name,
            Amount = legs.Sum(b => b.TotalPrice),
            Currency = outbound.Currency,
            ExpiresAt = clock.GetUtcNow().AddMinutes(Math.Max(30, options.Value.CheckoutExpiryMinutes)),
        };

        var manageUrl = links.ManageUrl(outbound);
        var session = await gateway.CreateCheckoutAsync(new CheckoutRequest(
            outbound.Reference,
            legs.Select(b => new CheckoutLine($"Transfer {b.Reference}", b.Describe(), b.TotalPrice)).ToList(),
            payment.Currency,
            client.Email ?? "",
            client.Language,
            manageUrl + "?paid=1",
            manageUrl,
            payment.ExpiresAt.Value,
            payment.Id.ToString()), cancellationToken);

        payment.CheckoutSessionId = session.Id;
        db.Payments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);
        return session.Url;
    }

    public async Task HandleWebhookAsync(string payload, string signature, CancellationToken cancellationToken)
    {
        var paymentEvent = gateway.ParseWebhook(payload, signature);
        if (paymentEvent is not null)
        {
            await HandleEventAsync(paymentEvent, cancellationToken);
        }
    }

    public async Task HandleEventAsync(PaymentEvent paymentEvent, CancellationToken cancellationToken)
    {
        var payment = await db.Payments
            .Include(p => p.Booking).ThenInclude(b => b.Client)
            .FirstOrDefaultAsync(p => p.CheckoutSessionId == paymentEvent.SessionId, cancellationToken);
        if (payment is null)
        {
            logger.LogWarning("Payment event {EventId} for unknown session {SessionId}", paymentEvent.EventId, paymentEvent.SessionId);
            return;
        }

        if (paymentEvent.Type == PaymentEventType.CheckoutCompleted)
        {
            await CompleteAsync(payment, cancellationToken);
        }
        else if (payment.Status == PaymentStatus.Pending)
        {
            // The booking stays "awaiting payment": the client can try again from the booking link.
            payment.Status = PaymentStatus.Failed;
            audit.Log(paymentEvent.Type == PaymentEventType.CheckoutExpired ? "payment.expired" : "payment.failed",
                nameof(Payment), payment.Id.ToString());
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task CompleteAsync(Payment payment, CancellationToken cancellationToken)
    {
        if (payment.Status is PaymentStatus.Succeeded or PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded)
        {
            return;
        }

        var details = await gateway.GetCheckoutAsync(payment.CheckoutSessionId!, cancellationToken);
        if (!details.Paid)
        {
            return;
        }
        if (details.AmountTotal != payment.Amount)
        {
            logger.LogError("Payment {PaymentId}: paid {Paid} but expected {Expected}", payment.Id, details.AmountTotal, payment.Amount);
        }

        payment.Status = PaymentStatus.Succeeded;
        payment.Method = details.Method;
        payment.ProviderTransactionId = details.PaymentIntentId;
        payment.PaidAt = clock.GetUtcNow();

        var legs = await db.LoadLegsAsync(payment.Booking, cancellationToken);
        foreach (var leg in legs.Where(b => b.Status == BookingStatus.AwaitingPayment))
        {
            leg.Status = BookingStatus.New;
        }
        audit.Log("payment.succeeded", nameof(Payment), payment.Id.ToString(),
            new { payment.Booking.Reference, payment.Amount, payment.Method });
        await db.SaveChangesAsync(cancellationToken);

        var invoice = await invoices.IssueInvoiceAsync(payment, cancellationToken);
        await emails.SendConfirmationAsync(payment.Booking, legs, invoice, cancellationToken);
    }
}
