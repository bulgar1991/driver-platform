using System.Net;
using System.Net.Http.Json;
using DriverPlatform.Application.Geo;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Tests.Infrastructure;

namespace DriverPlatform.Tests.Booking;

public sealed class QuoteApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Quote_prices_every_category_from_the_route_distance()
    {
        var quote = await factory.CreateClient().QuoteAsync(TestData.Trip());

        var km = quote.Outbound.DistanceKm;
        var sedan = quote.Vehicles.Single(v => v.Category == VehicleCategory.Sedan);
        var van = quote.Vehicles.Single(v => v.Category == VehicleCategory.Van);
        Assert.Equal(Math.Round(10m + km * 1.60m, 2), sedan.Total);
        Assert.Equal(Math.Round(15m + km * 2.20m, 2), van.Total);
        Assert.Empty(quote.Outbound.Surcharges);
        Assert.Equal(10m, quote.VatRatePercent);
    }

    [Fact]
    public async Task Night_pickup_on_a_weekday_adds_twenty_percent()
    {
        var trip = TestData.Trip() with { PickupAt = TestData.QuietPickup().Date.AddHours(23) };

        var quote = await factory.CreateClient().QuoteAsync(trip);

        Assert.Equal(20m, quote.Outbound.SurchargePercent);
        var sedan = quote.Vehicles.Single(v => v.Category == VehicleCategory.Sedan);
        Assert.Equal(Math.Round(sedan.Outbound.Fare * 1.2m, 2), sedan.Total);
    }

    [Fact]
    public async Task Large_groups_only_fit_the_van()
    {
        var trip = TestData.Trip() with { Passengers = 6, LargeLuggage = 6 };

        var quote = await factory.CreateClient().QuoteAsync(trip);

        Assert.False(quote.Vehicles.Single(v => v.Category == VehicleCategory.Sedan).Available);
        Assert.False(quote.Vehicles.Single(v => v.Category == VehicleCategory.Estate).Available);
        Assert.True(quote.Vehicles.Single(v => v.Category == VehicleCategory.Van).Available);
    }

    [Fact]
    public async Task Pickup_in_venice_moves_to_the_nearest_meeting_point()
    {
        var quote = await factory.CreateClient().QuoteAsync(TestData.Trip(TestData.VeniceRialto, TestData.VeniceAirport));

        Assert.Equal(ZoneType.NoCarAccess, quote.Outbound.PickupZone?.Type);
        Assert.Equal("Piazzale Roma", quote.Outbound.PickupMeetingPoint?.Name);
        // The route starts at the meeting point, not at the client's address.
        Assert.Contains(factory.Routing.Calls, c => c.From == new Coordinate(45.4381, 12.3180));
        // The client's exact address is kept for the driver.
        Assert.Equal(TestData.VeniceRialto.Address, quote.Outbound.Pickup.Address);
    }

    [Fact]
    public async Task Client_can_choose_another_meeting_point_but_not_an_unknown_one()
    {
        var client = factory.CreateClient();
        var trip = TestData.Trip(TestData.VeniceRialto, TestData.VeniceAirport);

        var tronchetto = await client.QuoteAsync(trip with { PickupMeetingPoint = "tronchetto" });
        var unknown = await client.PostAsJsonAsync("/api/quotes", trip with { PickupMeetingPoint = "San Marco" }, TestData.Json);

        Assert.Equal("Tronchetto", tronchetto.Outbound.PickupMeetingPoint?.Name);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task Dropoff_in_a_ztl_adds_the_access_fee()
    {
        var florenceStation = new Application.Quotes.PointInput { Address = "Firenze SMN", Latitude = 43.7764, Longitude = 11.2480 };
        var florenceAirport = new Application.Quotes.PointInput { Address = "Aeroporto di Firenze", Latitude = 43.8093, Longitude = 11.2027 };

        var quote = await factory.CreateClient().QuoteAsync(TestData.Trip(florenceAirport, florenceStation));

        var sedan = quote.Vehicles.Single(v => v.Category == VehicleCategory.Sedan);
        Assert.Equal(5m, sedan.Outbound.ZoneFeesTotal);
        Assert.Equal("Florence Airport arrivals", quote.Outbound.PickupMeetingPoint?.Name);
    }

    [Theory]
    [InlineData(1)]   // too soon
    [InlineData(400)] // too far ahead, in days
    public async Task Pickup_time_must_be_within_the_booking_window(int offset)
    {
        var now = ItalyTime.ToLocal(DateTimeOffset.UtcNow);
        var trip = TestData.Trip() with { PickupAt = offset == 1 ? now.AddHours(1) : now.AddDays(offset) };

        var response = await factory.CreateClient().PostAsJsonAsync("/api/quotes", trip, TestData.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Points_outside_italy_are_refused()
    {
        var paris = new Application.Quotes.PointInput { Address = "Paris", Latitude = 48.8566, Longitude = 2.3522 };

        var response = await factory.CreateClient().PostAsJsonAsync("/api/quotes", TestData.Trip(paris), TestData.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Address_search_lists_predefined_meeting_points_first()
    {
        var places = await factory.CreateClient()
            .GetFromJsonAsync<List<PlaceDto>>("/api/geo/search?q=Fiumicino", TestData.Json);

        Assert.Equal(PlaceKind.MeetingPoint, places![0].Kind);
        Assert.Contains("Fiumicino", places[0].Label);
    }
}
