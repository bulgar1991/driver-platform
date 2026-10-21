namespace DriverPlatform.Infrastructure.Payments;

public sealed class StripeOptions
{
    public const string SectionName = "Payments:Stripe";

    /// <summary>sk_test_… or sk_live_…; keep it in user secrets or the server environment, never in git.</summary>
    public string SecretKey { get; set; } = "";
    /// <summary>whsec_… from the webhook endpoint (or from "stripe listen" locally).</summary>
    public string WebhookSecret { get; set; } = "";
    /// <summary>Empty: Stripe shows the methods enabled in the dashboard (card, PayPal, …).</summary>
    public string[] PaymentMethodTypes { get; set; } = [];
}
