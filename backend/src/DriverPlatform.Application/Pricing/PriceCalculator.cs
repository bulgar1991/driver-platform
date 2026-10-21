using System.Globalization;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;

namespace DriverPlatform.Application.Pricing;

public sealed record PriceInput(
    VehicleRate Rate,
    decimal DistanceKm,
    IReadOnlyList<SurchargeRule> Surcharges,
    IReadOnlyList<Zone> FeeZones,
    int ChildSeats,
    int ExtraLuggage,
    bool NameSign,
    IReadOnlyDictionary<ExtraType, decimal> ExtraFees,
    decimal DiscountPercent = 0);

public sealed record LegPrice(
    decimal Fare,
    decimal SurchargePercent,
    decimal SurchargeTotal,
    decimal DiscountTotal,
    decimal ZoneFeesTotal,
    decimal ExtrasTotal,
    decimal Total,
    IReadOnlyList<PriceLine> Lines);

/// <summary>
/// Business plan §4: fare = base fee + km × rate; the day surcharges are added together and apply to the fare;
/// a return discount applies to the surcharged fare; zone fees and extras are fixed amounts on top.
/// All amounts include VAT.
/// </summary>
public static class PriceCalculator
{
    public static LegPrice Calculate(PriceInput input)
    {
        var lines = new List<PriceLine>();

        var fare = Money(input.Rate.BaseFee + input.DistanceKm * input.Rate.PricePerKm);
        lines.Add(new PriceLine
        {
            Kind = PriceLineKind.Fare,
            Label = Invariant($"{input.Rate.Category} · {input.DistanceKm:0.0} km"),
            Amount = fare,
        });

        var surchargePercent = input.Surcharges.Sum(s => s.Percent);
        var surchargeTotal = 0m;
        foreach (var rule in input.Surcharges)
        {
            var amount = Money(fare * rule.Percent / 100);
            surchargeTotal += amount;
            lines.Add(new PriceLine { Kind = PriceLineKind.Surcharge, Label = Invariant($"{rule.Name} +{rule.Percent:0.##}%"), Amount = amount });
        }

        var discountTotal = 0m;
        if (input.DiscountPercent > 0)
        {
            discountTotal = Money((fare + surchargeTotal) * input.DiscountPercent / 100);
            lines.Add(new PriceLine
            {
                Kind = PriceLineKind.Discount,
                Label = Invariant($"Return trip −{input.DiscountPercent:0.##}%"),
                Amount = -discountTotal,
            });
        }

        var zoneFeesTotal = 0m;
        foreach (var zone in input.FeeZones.Where(z => z.AccessFee > 0))
        {
            zoneFeesTotal += zone.AccessFee;
            lines.Add(new PriceLine { Kind = PriceLineKind.ZoneFee, Label = $"{zone.Name} access", Amount = zone.AccessFee });
        }

        var extrasTotal = 0m;
        void AddExtra(ExtraType type, int quantity, string label)
        {
            if (quantity <= 0 || !input.ExtraFees.TryGetValue(type, out var unit))
            {
                return;
            }
            var amount = Money(unit * quantity);
            extrasTotal += amount;
            lines.Add(new PriceLine { Kind = PriceLineKind.Extra, Label = label, Quantity = quantity, Amount = amount });
        }
        AddExtra(ExtraType.ChildSeat, input.ChildSeats, "Child seat");
        AddExtra(ExtraType.ExtraLuggage, input.ExtraLuggage, "Extra luggage");
        AddExtra(ExtraType.NameSign, input.NameSign ? 1 : 0, "Name sign");

        var total = fare + surchargeTotal - discountTotal + zoneFeesTotal + extrasTotal;
        return new LegPrice(fare, surchargePercent, surchargeTotal, discountTotal, zoneFeesTotal, extrasTotal, total, lines);
    }

    /// <summary>VAT contained in a VAT-inclusive amount.</summary>
    public static decimal VatIncluded(decimal total, decimal vatRatePercent) =>
        Money(total - total / (1 + vatRatePercent / 100));

    // Labels are stored on the booking, so they must not depend on the server's culture.
    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    public static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
