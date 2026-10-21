using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Abstractions;

public sealed record CheckoutLine(string Name, string? Description, decimal Amount);

/// <param name="IdempotencyKey">Same key, same session: protects against double clicks.</param>
public sealed record CheckoutRequest(
    string Reference,
    IReadOnlyList<CheckoutLine> Lines,
    string Currency,
    string CustomerEmail,
    string Locale,
    string SuccessUrl,
    string CancelUrl,
    DateTimeOffset ExpiresAt,
    string IdempotencyKey);

public sealed record CheckoutSession(string Id, string Url);

/// <param name="Paid">True once the money is captured.</param>
public sealed record CheckoutDetails(
    string SessionId,
    bool Paid,
    bool Expired,
    string? PaymentIntentId,
    PaymentMethod? Method,
    decimal AmountTotal);

public enum PaymentEventType
{
    CheckoutCompleted,
    CheckoutFailed,
    CheckoutExpired,
}

public sealed record PaymentEvent(string EventId, PaymentEventType Type, string SessionId);

public sealed record RefundResult(string Id, string Status);

/// <summary>Stripe in production; a local fake in development and tests.</summary>
public interface IPaymentGateway
{
    string Name { get; }

    Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken);

    /// <summary>Reads the session back from the processor; webhook payloads are only used as a hint.</summary>
    Task<CheckoutDetails> GetCheckoutAsync(string sessionId, CancellationToken cancellationToken);

    Task<RefundResult> RefundAsync(string paymentIntentId, decimal amount, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Verifies the signature. Returns null for event types we do not handle; throws if the signature is invalid.</summary>
    PaymentEvent? ParseWebhook(string payload, string signature);
}

public sealed class InvalidWebhookException(string message, Exception? inner = null) : Exception(message, inner);
