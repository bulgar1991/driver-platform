using System.Net;
using System.Net.Http.Json;
using DriverPlatform.Application.Admin;
using DriverPlatform.Application.Bookings;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Infrastructure.Persistence;
using DriverPlatform.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DriverPlatform.Tests.Booking;

public sealed class BookingApiTests
{
    [Fact]
    public async Task Booking_is_saved_awaiting_payment_with_the_quoted_price_and_client_ip()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var trip = TestData.Trip(TestData.VeniceRialto, TestData.VeniceAirport) with { NameSign = true };
        var quote = await client.QuoteAsync(trip);
        var sedan = quote.Vehicles.Single(v => v.Category == VehicleCategory.Sedan);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
        {
            Content = JsonContent.Create(TestData.Booking(trip, sedan.Total), options: TestData.Json),
        };
        request.Headers.Add("X-Forwarded-For", "203.0.113.7");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateBookingResponse>(TestData.Json);
        Assert.Matches("^MD-[2-9A-Z]{6}$", body!.Reference);
        Assert.Equal(sedan.Total, body.Total);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var booking = await db.Bookings.Include(b => b.Client).SingleAsync();
        Assert.Equal(BookingStatus.AwaitingPayment, booking.Status);
        Assert.Equal(BookingSource.Website, booking.Source);
        Assert.Equal("203.0.113.7", booking.ClientIp);
        Assert.Equal("Piazzale Roma", booking.PickupMeetingPoint?.Name);
        Assert.Equal("Maria Rossi", booking.NameSignText);
        Assert.Equal(sedan.Total, booking.TotalPrice);
        Assert.Equal(booking.TotalPrice, booking.PriceLines.Sum(l => l.Amount));
        Assert.Equal("+393331234567", booking.Client.Phone);
        Assert.NotNull(booking.Client.GdprConsentAt);
    }

    [Fact]
    public async Task Changed_price_returns_409_and_saves_nothing()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/bookings",
            TestData.Booking(TestData.Trip(), expectedTotal: 1m), TestData.Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Bookings.AnyAsync());
    }

    [Fact]
    public async Task Booking_without_privacy_consent_is_refused()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/bookings",
            TestData.Booking(TestData.Trip()) with { GdprConsent = false }, TestData.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Return_trip_creates_a_second_booking_with_the_return_discount()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();
        (await admin.PutAsJsonAsync("/api/admin/pricing/settings",
            new PricingSettingsDto { VatRatePercent = 10, ReturnDiscountPercent = 10, MinLeadTimeHours = 2 }, TestData.Json))
            .EnsureSuccessStatusCode();

        var outboundAt = TestData.QuietPickup();
        var trip = TestData.Trip(TestData.VeniceRialto, TestData.VeniceAirport) with
        {
            Return = new() { PickupAt = outboundAt.AddDays(7), FlightNumber = "fr 1234" },
        };
        var response = await factory.CreateClient().PostAsJsonAsync("/api/bookings", TestData.Booking(trip), TestData.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var bookings = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Bookings.ToListAsync();
        var outbound = bookings.Single(b => b.ReturnOfBookingId is null);
        var inbound = bookings.Single(b => b.ReturnOfBookingId == outbound.Id);
        // The return leg starts at the airport and ends at the Venice meeting point.
        Assert.Equal("Piazzale Roma", inbound.DropoffMeetingPoint?.Name);
        Assert.Equal("FR 1234", inbound.FlightNumber);
        Assert.Equal(0m, outbound.DiscountTotal);
        Assert.Equal(Math.Round((inbound.BaseFare + inbound.SurchargeTotal) * 0.10m, 2), inbound.DiscountTotal);
        Assert.Equal(outbound.ClientId, inbound.ClientId);
    }
}
