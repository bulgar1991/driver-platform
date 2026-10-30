using System.Net;
using System.Net.Http.Json;
using DriverPlatform.Application.Admin;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;
using DriverPlatform.Tests.Infrastructure;

namespace DriverPlatform.Tests.Booking;

/// <summary>Zones with their own price replace the normal fare of rides starting or ending in them.</summary>
public sealed class ZonePricingTests
{
    // Test trips go from Roma Termini to EUR.
    private static ZoneRequest Termini(ZonePricing pricing, params ZoneRate[] rates) => new()
    {
        Name = "Termini",
        Type = ZoneType.TrainStation,
        Shape = ZoneShape.Circle,
        CenterLatitude = TestData.RomeTermini.Latitude,
        CenterLongitude = TestData.RomeTermini.Longitude,
        RadiusMeters = 800,
        Pricing = pricing,
        Rates = [.. rates],
    };

    private static async Task<HttpClient> AdminWithZoneAsync(ApiFactory factory, ZoneRequest zone)
    {
        var admin = await factory.CreateAdminClientAsync();
        var created = await admin.PostAsJsonAsync("/api/admin/zones", zone, TestData.Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return admin;
    }

    [Fact]
    public async Task Fixed_price_replaces_the_fare_for_the_categories_that_have_one()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var before = await client.QuoteAsync(TestData.Trip());
        await AdminWithZoneAsync(factory, Termini(ZonePricing.FixedPrice, new ZoneRate { Category = VehicleCategory.Sedan, FixedPrice = 55 }));

        var quote = await client.QuoteAsync(TestData.Trip());

        var sedan = quote.Vehicles.Single(v => v.Category == VehicleCategory.Sedan);
        Assert.Equal(55m, sedan.Outbound.Fare);
        Assert.Contains("Termini", sedan.Outbound.Lines[0].Label);
        // Business has no zone price: still the normal rate.
        Assert.Equal(before.Vehicles.Single(v => v.Category == VehicleCategory.Business).Total,
            quote.Vehicles.Single(v => v.Category == VehicleCategory.Business).Total);
    }

    [Fact]
    public async Task Fixed_price_stops_at_its_distance_limit()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var before = await client.QuoteAsync(TestData.Trip());
        await AdminWithZoneAsync(factory, Termini(ZonePricing.FixedPrice, new ZoneRate { Category = VehicleCategory.Sedan, FixedPrice = 55 }) with { FixedPriceMaxKm = 1 });

        var quote = await client.QuoteAsync(TestData.Trip());

        Assert.Equal(before.Vehicles[0].Total, quote.Vehicles[0].Total);
    }

    [Fact]
    public async Task Zone_price_per_km_replaces_the_normal_rate_both_ways()
    {
        using var factory = new ApiFactory();
        await AdminWithZoneAsync(factory, Termini(ZonePricing.PerKm, new ZoneRate { Category = VehicleCategory.Sedan, BaseFee = 5, PricePerKm = 1 }));

        // Ending in the zone counts as much as starting there.
        var quote = await factory.CreateClient().QuoteAsync(TestData.Trip(TestData.RomeEur, TestData.RomeTermini));

        var sedan = quote.Vehicles.Single(v => v.Category == VehicleCategory.Sedan);
        Assert.Equal(Math.Round(5m + quote.Outbound.DistanceKm, 2), sedan.Outbound.Fare);
    }

    [Fact]
    public async Task The_smallest_zone_wins_where_zones_overlap()
    {
        using var factory = new ApiFactory();
        var admin = await AdminWithZoneAsync(factory, Termini(ZonePricing.FixedPrice, new ZoneRate { Category = VehicleCategory.Sedan, FixedPrice = 55 }));
        var city = Termini(ZonePricing.PerKm, new ZoneRate { Category = VehicleCategory.Sedan, BaseFee = 1, PricePerKm = 0.5m }) with { Name = "Rome", RadiusMeters = 20_000 };
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/admin/zones", city, TestData.Json)).StatusCode);

        var quote = await factory.CreateClient().QuoteAsync(TestData.Trip());

        Assert.Equal(55m, quote.Vehicles.Single(v => v.Category == VehicleCategory.Sedan).Outbound.Fare);
    }

    [Fact]
    public async Task Zone_prices_are_checked()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();
        var sedan = new ZoneRate { Category = VehicleCategory.Sedan, FixedPrice = 55 };

        var none = await admin.PostAsJsonAsync("/api/admin/zones", Termini(ZonePricing.FixedPrice), TestData.Json);
        var twice = await admin.PostAsJsonAsync("/api/admin/zones", Termini(ZonePricing.FixedPrice, sedan, sedan), TestData.Json);
        var halfPerKm = await admin.PostAsJsonAsync("/api/admin/zones",
            Termini(ZonePricing.PerKm, new ZoneRate { Category = VehicleCategory.Sedan, BaseFee = 5 }), TestData.Json);
        var ok = await admin.PostAsJsonAsync("/api/admin/zones", Termini(ZonePricing.FixedPrice, sedan), TestData.Json);

        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, twice.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, halfPerKm.StatusCode);
        var saved = (await ok.Content.ReadFromJsonAsync<ZoneAdminDto>(TestData.Json))!;
        Assert.Equal(ZonePricing.FixedPrice, saved.Pricing);
        Assert.Equal(55m, Assert.Single(saved.Rates).FixedPrice);
    }
}
