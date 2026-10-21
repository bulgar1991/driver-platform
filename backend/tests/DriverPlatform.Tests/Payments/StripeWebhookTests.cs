using System.Security.Cryptography;
using System.Text;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Infrastructure.Payments;
using Microsoft.Extensions.Options;

namespace DriverPlatform.Tests.Payments;

public sealed class StripeWebhookTests
{
    private const string Secret = "whsec_test_secret";

    private static readonly StripePaymentGateway Gateway = new(Options.Create(new StripeOptions
    {
        SecretKey = "sk_test_unused",
        WebhookSecret = Secret,
    }));

    private static string Payload(string type) => $$"""
        {
          "id": "evt_123",
          "object": "event",
          "api_version": "2025-01-27.acacia",
          "created": 1700000000,
          "type": "{{type}}",
          "data": { "object": { "id": "cs_test_abc", "object": "checkout.session", "payment_status": "paid" } }
        }
        """;

    /// <summary>Stripe's scheme: v1 = HMAC-SHA256(secret, "{timestamp}.{payload}").</summary>
    private static string Sign(string payload, string secret = Secret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return $"t={timestamp},v1={Convert.ToHexStringLower(mac)}";
    }

    [Theory]
    [InlineData("checkout.session.completed", PaymentEventType.CheckoutCompleted)]
    [InlineData("checkout.session.async_payment_succeeded", PaymentEventType.CheckoutCompleted)]
    [InlineData("checkout.session.async_payment_failed", PaymentEventType.CheckoutFailed)]
    [InlineData("checkout.session.expired", PaymentEventType.CheckoutExpired)]
    public void Signed_checkout_events_are_accepted(string type, PaymentEventType expected)
    {
        var payload = Payload(type);

        var parsed = Gateway.ParseWebhook(payload, Sign(payload));

        Assert.Equal(expected, parsed?.Type);
        Assert.Equal("cs_test_abc", parsed?.SessionId);
    }

    [Fact]
    public void Wrong_signature_is_rejected()
    {
        var payload = Payload("checkout.session.completed");

        Assert.Throws<InvalidWebhookException>(() => Gateway.ParseWebhook(payload, Sign(payload, "whsec_other")));
    }

    [Fact]
    public void Other_event_types_are_ignored()
    {
        var payload = Payload("checkout.session.created");

        Assert.Null(Gateway.ParseWebhook(payload, Sign(payload)));
    }
}
