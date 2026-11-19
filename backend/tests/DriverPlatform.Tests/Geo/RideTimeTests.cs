using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Quotes;
using DriverPlatform.Tests.Infrastructure;

namespace DriverPlatform.Tests.Geo;

public sealed class RideTimeTests
{
    [Theory]
    [InlineData(51.5072, -0.1276, "Europe/London")]
    [InlineData(41.9028, 12.4964, "Europe/Rome")]
    [InlineData(47.3769, 8.5417, "Europe/Zurich")]
    [InlineData(38.7223, -9.1393, "Europe/Lisbon")]
    [InlineData(37.9838, 23.7275, "Europe/Athens")]
    [InlineData(60.1699, 24.9384, "Europe/Helsinki")]
    [InlineData(28.1235, -15.4363, "Atlantic/Canary")]
    public void Finds_the_time_zone_of_a_pickup(double latitude, double longitude, string zone) =>
        Assert.Equal(zone, RideTime.ZoneAt(latitude, longitude).Id);

    [Fact]
    public void Ten_in_the_morning_is_local_time_at_the_pickup()
    {
        // A winter day: London on UTC, Rome one hour ahead, Athens two.
        var local = new DateTime(2027, 1, 12, 10, 0, 0);

        Assert.Equal(new DateTimeOffset(2027, 1, 12, 10, 0, 0, TimeSpan.Zero), RideTime.ToUtc(local, new Coordinate(51.5072, -0.1276)));
        Assert.Equal(new DateTimeOffset(2027, 1, 12, 9, 0, 0, TimeSpan.Zero), RideTime.ToUtc(local, new Coordinate(41.9028, 12.4964)));
        Assert.Equal(new DateTimeOffset(2027, 1, 12, 8, 0, 0, TimeSpan.Zero), RideTime.ToUtc(local, new Coordinate(37.9838, 23.7275)));
    }

    [Fact]
    public void The_hour_skipped_in_spring_does_not_exist_at_the_pickup()
    {
        // 28 March 2027: UK clocks go from 01:00 to 02:00, Italian ones from 02:00 to 03:00.
        Assert.Null(RideTime.ToUtc(new DateTime(2027, 3, 28, 1, 30, 0), new Coordinate(51.5072, -0.1276)));
        Assert.NotNull(RideTime.ToUtc(new DateTime(2027, 3, 28, 1, 30, 0), new Coordinate(41.9028, 12.4964)));
    }
}

public sealed class RideTimeApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly PointInput LondonKingsCross = new() { Address = "King's Cross, London", Latitude = 51.5308, Longitude = -0.1238 };
    private static readonly PointInput Heathrow = new() { Address = "Heathrow Airport", Latitude = 51.4700, Longitude = -0.4543 };

    [Fact]
    public async Task A_london_pickup_is_priced_and_kept_in_uk_time()
    {
        var pickup = TestData.QuietPickup();
        var trip = TestData.Trip(LondonKingsCross, Heathrow) with { PickupAt = pickup };

        var quote = await factory.CreateClient().QuoteAsync(trip);

        var london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        Assert.Equal(pickup, quote.Outbound.PickupAt);
        Assert.Equal(new DateTimeOffset(pickup, london.GetUtcOffset(pickup)).ToUniversalTime(), quote.Outbound.PickupAtUtc);
    }
}
