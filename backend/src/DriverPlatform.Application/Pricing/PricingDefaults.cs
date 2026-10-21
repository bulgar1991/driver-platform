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
    public const string Currency = "EUR";

    public static async Task<PricingSettings> LoadSettingsAsync(this IAppDbContext db, CancellationToken cancellationToken) =>
        await db.PricingSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
        ?? new PricingSettings { VatRatePercent = VatRatePercent, MinLeadTimeHours = MinLeadTimeHours };
}
