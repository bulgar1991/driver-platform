using DriverPlatform.Application.Abstractions;
using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Pricing;

public static class PricingDefaults
{
    /// <summary>Italian VAT on passenger transport (10%) until the accountant confirms otherwise.</summary>
    public const decimal VatRatePercent = 10m;
    public const int MinLeadTimeHours = 2;
    public const int MaxDaysAhead = 365;

    /// <summary>
    /// The currency of the site (Site:Currency, per server: "EUR" by default, "CHF" in Switzerland). Tariffs
    /// are entered in it and new bookings are priced and paid in it; a booking keeps the currency it was made in.
    /// </summary>
    public static string Currency { get; private set; } = "EUR";

    /// <summary>Set once at startup. Only a three-letter ISO code (EUR, CHF…); anything else stops the app at once.</summary>
    public static void UseCurrency(string? code)
    {
        var value = string.IsNullOrWhiteSpace(code) ? "EUR" : code.Trim().ToUpperInvariant();
        if (value.Length != 3 || !value.All(char.IsAsciiLetterUpper))
        {
            throw new InvalidOperationException($"Site:Currency must be a three-letter ISO code such as EUR or CHF, not \"{code}\".");
        }
        Currency = value;
    }

    public static async Task<PricingSettings> LoadSettingsAsync(this IAppDbContext db, CancellationToken cancellationToken) =>
        await db.PricingSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
        ?? new PricingSettings { VatRatePercent = VatRatePercent, MinLeadTimeHours = MinLeadTimeHours };
}
