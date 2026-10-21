using System.Globalization;
using DriverPlatform.Application.Pricing;
using DriverPlatform.Application.Quotes;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;

namespace DriverPlatform.Application.Partners;

/// <summary>Business plan §4: B2B partners and Booking can have an agreed rate on top of the public price.</summary>
public static class PartnerPricing
{
    public static QuoteResponse Adjust(QuoteResponse quote, decimal percent) => percent == 0
        ? quote
        : quote with
        {
            Vehicles = quote.Vehicles.Select(v =>
            {
                var outbound = Adjust(v.Outbound, percent);
                var inbound = v.Return is null ? null : Adjust(v.Return, percent);
                var total = outbound.Total + (inbound?.Total ?? 0);
                return v with
                {
                    Outbound = outbound,
                    Return = inbound,
                    Total = total,
                    VatIncluded = PriceCalculator.VatIncluded(total, quote.VatRatePercent),
                };
            }).ToList(),
        };

    private static LegPrice Adjust(LegPrice price, decimal percent)
    {
        var amount = PriceCalculator.Money(price.Total * percent / 100);
        var line = new PriceLine
        {
            Kind = amount < 0 ? PriceLineKind.Discount : PriceLineKind.Surcharge,
            Label = string.Create(CultureInfo.InvariantCulture, $"Partner rate {(percent > 0 ? "+" : "−")}{Math.Abs(percent):0.##}%"),
            Amount = amount,
        };
        return price with
        {
            DiscountTotal = price.DiscountTotal + (amount < 0 ? -amount : 0),
            SurchargeTotal = price.SurchargeTotal + (amount > 0 ? amount : 0),
            Total = price.Total + amount,
            Lines = [.. price.Lines, line],
        };
    }
}
