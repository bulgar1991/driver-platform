namespace DriverPlatform.Api.Extensions;

/// <summary>
/// Refuses to start in Production with development settings (fake payments, dev keys, emails written to
/// disk…). Each problem is listed, so a bad deployment fails at once instead of misbehaving later.
/// </summary>
public static class ProductionChecks
{
    private static readonly string[] DevelopmentKeys =
    [
        "dev-only-signing-key-change-me-0123456789abcdef",
        "dev-only-links-key-change-me-0123456789abcdef",
    ];

    public static void EnsureProductionReady(this WebApplication app)
    {
        if (!app.Environment.IsProduction())
        {
            return;
        }
        var c = app.Configuration;
        var errors = new List<string>();
        var warnings = new List<string>();

        foreach (var key in new[] { "Jwt:SigningKey", "Links:SigningKey" })
        {
            var value = c[key] ?? "";
            if (value.Length < 32 || DevelopmentKeys.Contains(value))
            {
                errors.Add($"{key} must be a unique random value of at least 32 characters.");
            }
        }
        if (c["Jwt:SigningKey"] is { Length: > 0 } jwt && jwt == c["Links:SigningKey"])
        {
            errors.Add("Jwt:SigningKey and Links:SigningKey must be different.");
        }
        if (c["Payments:Provider"] != "Stripe")
        {
            errors.Add("Payments:Provider must be \"Stripe\" (the fake provider marks bookings paid without payment).");
        }
        if (!(c["Payments:FrontendBaseUrl"] ?? "").StartsWith("https://", StringComparison.Ordinal))
        {
            errors.Add("Payments:FrontendBaseUrl must be the public https:// address of the site.");
        }
        if (!string.IsNullOrWhiteSpace(c["Email:PickupDirectory"]) || string.IsNullOrWhiteSpace(c["Email:Host"]) || string.IsNullOrWhiteSpace(c["Email:FromAddress"]))
        {
            errors.Add("Email:Host and Email:FromAddress are required, and Email:PickupDirectory must be empty.");
        }
        if (string.IsNullOrWhiteSpace(c["Company:VatNumber"]) || string.IsNullOrWhiteSpace(c["Company:Address"]))
        {
            errors.Add("Company:VatNumber and Company:Address are printed on invoices and are required.");
        }
        if (c.GetSection("Cors:AllowedOrigins").Get<string[]>()?.Any(o => !o.StartsWith("https://", StringComparison.Ordinal)) == true)
        {
            errors.Add("Cors:AllowedOrigins may only list https:// origins.");
        }

        if (c["WhatsApp:Provider"] != "Meta")
        {
            warnings.Add("WhatsApp:Provider is not \"Meta\": WhatsApp messages are only logged, not sent.");
        }
        if (!string.IsNullOrEmpty(c["Bootstrap:AdminPassword"]))
        {
            warnings.Add("Bootstrap:AdminPassword is set: remove it once the first admin has signed in and changed it.");
        }
        if (c.GetValue<bool>("Database:MigrateOnStartup"))
        {
            warnings.Add("Database:MigrateOnStartup is on: run a single API instance while it is.");
        }

        foreach (var warning in warnings)
        {
            app.Logger.LogWarning("Production check: {Warning}", warning);
        }
        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Production configuration is not ready:\n - " + string.Join("\n - ", errors));
        }
    }
}
