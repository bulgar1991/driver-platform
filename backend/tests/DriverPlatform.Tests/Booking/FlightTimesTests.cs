using System.Net;
using System.Net.Http.Json;
using DriverPlatform.Application.Dispatch;
using DriverPlatform.Infrastructure.Persistence;
using DriverPlatform.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DriverPlatform.Tests.Booking;

public sealed class FlightTimesTests
{
    [Fact]
    public async Task Client_gives_takeoff_and_landing_for_each_leg()
    {
        using var factory = new ApiFactory();
        var outboundAt = TestData.QuietPickup();
        var returnAt = outboundAt.AddDays(7);
        var trip = TestData.Trip(TestData.VeniceRialto, TestData.VeniceAirport) with
        {
            Return = new()
            {
                PickupAt = returnAt,
                FlightNumber = "BA2584",
                // London time on the ticket, landing in Venice.
                FlightDepartsAt = returnAt.AddHours(-3),
                FlightLandsAt = returnAt.AddMinutes(-20),
            },
        };
        var request = TestData.Booking(trip) with
        {
            FlightNumber = "fr 1234",
            FlightDepartsAt = outboundAt.AddHours(3).AddSeconds(42),
            FlightLandsAt = outboundAt.AddHours(5),
        };

        var response = await factory.CreateClient().PostAsJsonAsync("/api/bookings", request, TestData.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var bookings = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Bookings.ToListAsync();
        var outbound = bookings.Single(b => b.ReturnOfBookingId is null);
        var inbound = bookings.Single(b => b.ReturnOfBookingId is not null);
        Assert.Equal(outboundAt.AddHours(3), outbound.FlightDepartsAt); // to the minute
        Assert.Equal(outboundAt.AddHours(5), outbound.FlightLandsAt);
        Assert.Equal("BA2584", inbound.FlightNumber);
        Assert.Equal(returnAt.AddHours(-3), inbound.FlightDepartsAt);
        Assert.Equal(returnAt.AddMinutes(-20), inbound.FlightLandsAt);
    }

    [Fact]
    public async Task Flight_on_another_day_than_the_ride_is_refused()
    {
        using var factory = new ApiFactory();
        var trip = TestData.Trip();
        var request = TestData.Booking(trip) with { FlightNumber = "FR1234", FlightLandsAt = trip.PickupAt.AddDays(-30) };

        var response = await factory.CreateClient().PostAsJsonAsync("/api/bookings", request, TestData.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Bookings.AnyAsync());
    }

    [Fact]
    public async Task Dispatcher_adds_and_changes_the_flight_later()
    {
        using var factory = new ApiFactory();
        var staff = await factory.CreateDispatcherClientAsync();
        var trip = TestData.Trip();
        var booked = await factory.CreateClient().BookAndPayAsync(trip);
        var id = (await staff.GetFromJsonAsync<PagedResult<BookingListItem>>(
            $"/api/admin/bookings?search={booked.Reference}", TestData.Json))!.Items.Single().Id;

        var response = await staff.PutAsJsonAsync($"/api/admin/bookings/{id}/flight",
            new BookingFlightRequest { FlightNumber = "az 1466", FlightDepartsAt = trip.PickupAt.AddHours(-2), FlightLandsAt = trip.PickupAt.AddMinutes(-15) },
            TestData.Json);
        var detail = await response.Content.ReadFromJsonAsync<BookingDetail>(TestData.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("AZ 1466", detail!.FlightNumber);
        Assert.Equal(trip.PickupAt.AddHours(-2), detail.FlightDepartsAt);
        Assert.Equal(trip.PickupAt.AddMinutes(-15), detail.FlightLandsAt);

        var tooFar = await staff.PutAsJsonAsync($"/api/admin/bookings/{id}/flight",
            new BookingFlightRequest { FlightNumber = "AZ1466", FlightLandsAt = trip.PickupAt.AddDays(3) }, TestData.Json);
        Assert.Equal(HttpStatusCode.BadRequest, tooFar.StatusCode);

        var cleared = await staff.PutAsJsonAsync($"/api/admin/bookings/{id}/flight", new BookingFlightRequest(), TestData.Json);
        detail = await cleared.Content.ReadFromJsonAsync<BookingDetail>(TestData.Json);
        Assert.Null(detail!.FlightNumber);
        Assert.Null(detail.FlightDepartsAt);
        Assert.Null(detail.FlightLandsAt);
    }
}
