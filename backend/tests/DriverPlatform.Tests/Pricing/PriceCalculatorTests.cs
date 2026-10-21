using DriverPlatform.Application.Pricing;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Tests.Pricing;

public sealed class PriceCalculatorTests
{
    private static readonly VehicleRate Sedan = new() { Category = VehicleCategory.Sedan, BaseFee = 10m, PricePerKm = 1.60m };

    private static readonly Dictionary<ExtraType, decimal> Extras = new()
    {
        [ExtraType.ChildSeat] = 10m,
        [ExtraType.ExtraLuggage] = 5m,
        [ExtraType.NameSign] = 10m,
    };

    private static SurchargeRule Rule(string name, decimal percent) => new() { Name = name, Percent = percent };

    private static PriceInput Input(params SurchargeRule[] surcharges) =>
        new(Sedan, 30m, surcharges, [], 0, 0, false, Extras);

    // Business plan §4: 30 km by sedan.
    [Theory]
    [InlineData(0, "58.00")]
    [InlineData(10, "63.80")]
    [InlineData(20, "69.60")]
    [InlineData(25, "72.50")]
    public void Matches_the_examples_in_the_business_plan(int percent, string expected)
    {
        var price = PriceCalculator.Calculate(Input(percent == 0 ? [] : [Rule("Day", percent)]));

        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), price.Total);
    }

    [Fact]
    public void Surcharges_are_added_together()
    {
        // Saturday night in August: weekend 10% + night 20% + peak 25% = 55% of 58.00.
        var price = PriceCalculator.Calculate(Input(Rule("Weekend", 10), Rule("Night", 20), Rule("Peak", 25)));

        Assert.Equal(55m, price.SurchargePercent);
        Assert.Equal(31.90m, price.SurchargeTotal);
        Assert.Equal(89.90m, price.Total);
    }

    [Fact]
    public void Extras_and_zone_fees_are_fixed_and_not_surcharged()
    {
        var ztl = new Zone { Name = "Florence ZTL", AccessFee = 5m };
        var price = PriceCalculator.Calculate(new PriceInput(
            Sedan, 30m, [Rule("Night", 20)], [ztl], ChildSeats: 2, ExtraLuggage: 1, NameSign: true, Extras));

        Assert.Equal(69.60m, price.Fare + price.SurchargeTotal);
        Assert.Equal(5m, price.ZoneFeesTotal);
        Assert.Equal(35m, price.ExtrasTotal); // 2 × 10 + 5 + 10
        Assert.Equal(109.60m, price.Total);
        Assert.Equal(price.Total, price.Lines.Sum(l => l.Amount));
    }

    [Fact]
    public void Return_discount_applies_to_the_ride_only()
    {
        var price = PriceCalculator.Calculate(new PriceInput(
            Sedan, 30m, [Rule("Weekend", 10)], [], 1, 0, false, Extras, DiscountPercent: 10));

        Assert.Equal(6.38m, price.DiscountTotal); // 10% of 63.80
        Assert.Equal(67.42m, price.Total);        // 63.80 − 6.38 + 10
        Assert.Equal(price.Total, price.Lines.Sum(l => l.Amount));
    }

    [Fact]
    public void Labels_do_not_depend_on_the_server_culture()
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("it-IT");
            var price = PriceCalculator.Calculate(new PriceInput(Sedan, 13.5m, [Rule("Night", 20.5m)], [], 0, 0, false, Extras));

            Assert.Equal("Sedan · 13.5 km", price.Lines[0].Label);
            Assert.Equal("Night +20.5%", price.Lines[1].Label);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void Vat_included_is_extracted_from_the_total()
    {
        Assert.Equal(10m, PriceCalculator.VatIncluded(110m, 10m));
    }
}
