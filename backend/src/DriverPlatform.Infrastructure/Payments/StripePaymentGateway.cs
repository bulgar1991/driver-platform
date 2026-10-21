using DriverPlatform.Application.Abstractions;
using PaymentMethod = DriverPlatform.Domain.Enums.PaymentMethod;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace DriverPlatform.Infrastructure.Payments;

/// <summary>Stripe Checkout: Stripe hosts the payment page, handles strong customer authentication (3-D Secure) and PayPal.</summary>
public sealed class StripePaymentGateway(IOptions<StripeOptions> options) : IPaymentGateway
{
    private readonly StripeClient _client = new(options.Value.SecretKey);

    public string Name => "Stripe";

    public async Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        var create = new SessionCreateOptions
        {
            Mode = "payment",
            CustomerEmail = string.IsNullOrEmpty(request.CustomerEmail) ? null : request.CustomerEmail,
            ClientReferenceId = request.Reference,
            Locale = request.Locale is "en" or "it" or "fr" or "de" or "es" or "ro" ? request.Locale : "auto",
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            ExpiresAt = request.ExpiresAt.UtcDateTime,
            PaymentMethodTypes = options.Value.PaymentMethodTypes.Length > 0 ? [.. options.Value.PaymentMethodTypes] : null,
            LineItems = request.Lines.Select(line => new SessionLineItemOptions
            {
                Quantity = 1,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = request.Currency.ToLowerInvariant(),
                    UnitAmount = ToCents(line.Amount),
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = line.Name,
                        Description = line.Description,
                    },
                },
            }).ToList(),
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                Description = $"Magnus Drive {request.Reference}",
                Metadata = new Dictionary<string, string> { ["reference"] = request.Reference },
            },
            Metadata = new Dictionary<string, string> { ["reference"] = request.Reference },
        };

        var session = await new SessionService(_client).CreateAsync(
            create, new RequestOptions { IdempotencyKey = request.IdempotencyKey }, cancellationToken);
        return new CheckoutSession(session.Id, session.Url);
    }

    public async Task<CheckoutDetails> GetCheckoutAsync(string sessionId, CancellationToken cancellationToken)
    {
        var session = await new SessionService(_client).GetAsync(
            sessionId,
            new SessionGetOptions { Expand = ["payment_intent.latest_charge"] },
            cancellationToken: cancellationToken);

        var methodType = session.PaymentIntent?.LatestCharge?.PaymentMethodDetails?.Type;
        return new CheckoutDetails(
            session.Id,
            Paid: session.PaymentStatus == "paid",
            Expired: session.Status == "expired",
            session.PaymentIntentId,
            methodType switch
            {
                "card" => PaymentMethod.Card,
                "paypal" => PaymentMethod.PayPal,
                _ => null,
            },
            (session.AmountTotal ?? 0) / 100m);
    }

    public async Task<RefundResult> RefundAsync(string paymentIntentId, decimal amount, string idempotencyKey, CancellationToken cancellationToken)
    {
        var refund = await new RefundService(_client).CreateAsync(
            new RefundCreateOptions { PaymentIntent = paymentIntentId, Amount = ToCents(amount) },
            new RequestOptions { IdempotencyKey = idempotencyKey },
            cancellationToken);
        return new RefundResult(refund.Id, refund.Status);
    }

    public PaymentEvent? ParseWebhook(string payload, string signature)
    {
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                payload, signature, options.Value.WebhookSecret, throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            throw new InvalidWebhookException("Invalid Stripe signature.", ex);
        }

        if (stripeEvent.Data.Object is not Session session)
        {
            return null;
        }
        PaymentEventType? type = stripeEvent.Type switch
        {
            EventTypes.CheckoutSessionCompleted or EventTypes.CheckoutSessionAsyncPaymentSucceeded => PaymentEventType.CheckoutCompleted,
            EventTypes.CheckoutSessionAsyncPaymentFailed => PaymentEventType.CheckoutFailed,
            EventTypes.CheckoutSessionExpired => PaymentEventType.CheckoutExpired,
            _ => null,
        };
        return type is null ? null : new PaymentEvent(stripeEvent.Id, type.Value, session.Id);
    }

    private static long ToCents(decimal amount) => (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero);
}
