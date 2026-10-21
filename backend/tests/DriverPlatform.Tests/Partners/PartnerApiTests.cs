using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using DriverPlatform.Application.Dispatch;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Partners;
using DriverPlatform.Application.Quotes;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Infrastructure.Partners;
using DriverPlatform.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DriverPlatform.Tests.Partners;

public sealed class PartnerApiTests
{
    private sealed record PartnerSetup(ApiFactory Factory, HttpClient Admin, HttpClient Api, PartnerDto Partner, string Key);

    private static async Task<PartnerSetup> SetupAsync(PartnerRequest? request = null)
    {
        var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();
        var partner = await Read<PartnerDto>(await admin.PostAsJsonAsync("/api/admin/partners", request ?? new PartnerRequest
        {
            Name = "Hotel Danieli",
            ContactEmail = "concierge@hotel.test",
            PriceAdjustmentPercent = -10,
            CommissionPercent = 5,
        }, TestData.Json), HttpStatusCode.Created);
        var key = await Read<PartnerApiKeyResponse>(await admin.PostAsync($"/api/admin/partners/{partner.Id}/api-key", null));

        var api = factory.CreateClient();
        api.DefaultRequestHeaders.Add("X-Api-Key", key.ApiKey);
        return new PartnerSetup(factory, admin, api, partner, key.ApiKey);
    }

    private static PartnerBookingRequest Booking(string externalReference, QuoteRequest? trip = null) => new()
    {
        ExternalReference = externalReference,
        Trip = trip ?? TestData.Trip(TestData.VeniceRialto, TestData.VeniceAirport),
        VehicleCategory = VehicleCategory.Sedan,
        Passenger = new PassengerInput { FirstName = "John", LastName = "Smith", Phone = "+44 7700 900123" },
        FlightNumber = "BA2584",
    };

    private static async Task<T> Read<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(TestData.Json))!;
    }

    [Fact]
    public async Task Api_key_is_required_and_can_be_revoked()
    {
        var s = await SetupAsync();
        using var _ = s.Factory;
        var anonymous = s.Factory.CreateClient();
        var forged = s.Factory.CreateClient();
        forged.DefaultRequestHeaders.Add("X-Api-Key", s.Key[..^4] + "AAAA");

        Assert.Matches("^md_[a-z2-9]{8}_[A-Za-z0-9_-]{43}$", s.Key);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/partner/v1/bookings")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await forged.GetAsync("/api/partner/v1/bookings")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Api.GetAsync("/api/partner/v1/bookings")).StatusCode);
        // A staff token does not open the partner API.
        Assert.Equal(HttpStatusCode.Unauthorized, (await s.Admin.GetAsync("/api/partner/v1/bookings")).StatusCode);

        await s.Admin.DeleteAsync($"/api/admin/partners/{s.Partner.Id}/api-key");
        Assert.Equal(HttpStatusCode.Unauthorized, (await s.Api.GetAsync("/api/partner/v1/bookings")).StatusCode);
    }

    [Fact]
    public async Task Partner_quotes_use_the_agreed_rate()
    {
        var s = await SetupAsync();
        using var _ = s.Factory;
        var trip = TestData.Trip();

        var publicQuote = await s.Factory.CreateClient().QuoteAsync(trip);
        var partnerQuote = await Read<QuoteResponse>(await s.Api.PostAsJsonAsync("/api/partner/v1/quotes", trip, TestData.Json));

        var publicSedan = publicQuote.Vehicles.Single(v => v.Category == VehicleCategory.Sedan);
        var partnerSedan = partnerQuote.Vehicles.Single(v => v.Category == VehicleCategory.Sedan);
        Assert.Equal(Math.Round(publicSedan.Total * 0.9m, 2), partnerSedan.Total);
        Assert.Contains(partnerSedan.Outbound.Lines, l => l.Label == "Partner rate −10%");
        Assert.Equal(partnerSedan.Total, partnerSedan.Outbound.Lines.Sum(l => l.Amount));
    }

    [Fact]
    public async Task Booking_is_created_once_per_external_reference_and_goes_straight_to_dispatch()
    {
        var s = await SetupAsync();
        using var _ = s.Factory;

        var created = await Read<PartnerBooking>(await s.Api.PostAsJsonAsync("/api/partner/v1/bookings", Booking("DAN-1001"), TestData.Json), HttpStatusCode.Created);
        var retried = await Read<PartnerBooking>(await s.Api.PostAsJsonAsync("/api/partner/v1/bookings", Booking("DAN-1001"), TestData.Json));
        var byExternal = await Read<PartnerBooking>(await s.Api.GetAsync("/api/partner/v1/bookings/DAN-1001"));

        Assert.Equal(BookingStatus.New, created.Status);
        Assert.Equal(created.Reference, retried.Reference);
        Assert.Equal(created.Reference, byExternal.Reference);
        Assert.Equal("Piazzale Roma", created.PickupMeetingPoint?.Name);
        Assert.Null(created.Driver);

        // The dispatcher sees it with the partner's name, without any payment step.
        var staff = await s.Factory.CreateDispatcherClientAsync();
        var list = await staff.GetFromJsonAsync<PagedResult<BookingListItem>>("/api/admin/bookings?unassigned=true", TestData.Json);
        var item = Assert.Single(list!.Items);
        Assert.Equal("Hotel Danieli", item.PartnerName);
        Assert.Equal(BookingSource.Partner, item.Source);
        Assert.Empty(s.Factory.Emails.Sent);

        // Another partner's key does not find it.
        var otherPartner = await Read<PartnerDto>(await s.Admin.PostAsJsonAsync("/api/admin/partners",
            new PartnerRequest { Name = "Other", ContactEmail = "x@other.test" }, TestData.Json), HttpStatusCode.Created);
        var otherKey = await Read<PartnerApiKeyResponse>(await s.Admin.PostAsync($"/api/admin/partners/{otherPartner.Id}/api-key", null));
        var otherApi = s.Factory.CreateClient();
        otherApi.DefaultRequestHeaders.Add("X-Api-Key", otherKey.ApiKey);
        Assert.Equal(HttpStatusCode.NotFound, (await otherApi.GetAsync("/api/partner/v1/bookings/DAN-1001")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherApi.GetAsync($"/api/partner/v1/bookings/{created.Reference}")).StatusCode);
    }

    [Fact]
    public async Task Partner_sees_the_driver_once_confirmed_and_can_cancel_before_pickup()
    {
        var s = await SetupAsync(new PartnerRequest
        {
            Name = "Booking.com",
            Kind = PartnerKind.Booking,
            ContactEmail = "transfers@booking.test",
            NotifyPassengers = false,
        });
        using var _ = s.Factory;
        var booking = await Read<PartnerBooking>(await s.Api.PostAsJsonAsync("/api/partner/v1/bookings", Booking("BKG-77"), TestData.Json), HttpStatusCode.Created);

        var staff = await s.Factory.CreateDispatcherClientAsync();
        var today = DateOnly.FromDateTime(ItalyTime.ToLocal(DateTimeOffset.UtcNow));
        var driver = (await (await staff.PostAsJsonAsync("/api/admin/drivers", new DriverRequest
        {
            FirstName = "Giulia", LastName = "Ferrari", Phone = "+393400000002",
            Documents = [new DriverDocumentDto { Type = DriverDocumentType.NccLicence, ExpiresOn = today.AddYears(2) }],
        }, TestData.Json)).Content.ReadFromJsonAsync<DriverDto>(TestData.Json))!;
        var car = (await (await staff.PostAsJsonAsync("/api/admin/vehicles", new VehicleRequest
        {
            PlateNumber = "GB456DE", Make = "Mercedes-Benz", Model = "E-Class", Color = "Black", Category = VehicleCategory.Sedan,
            Seats = 3, LuggageCapacity = 3, InsuranceExpiresOn = today.AddYears(1), InspectionExpiresOn = today.AddYears(1),
        }, TestData.Json)).Content.ReadFromJsonAsync<VehicleDto>(TestData.Json))!;
        var id = (await staff.GetFromJsonAsync<PagedResult<BookingListItem>>($"/api/admin/bookings?search={booking.Reference}", TestData.Json))!.Items.Single().Id;
        var shift = (await (await staff.PostAsJsonAsync("/api/admin/shifts", new ShiftRequest { DriverId = driver.Id, VehicleId = car.Id, BookingIds = [id] }, TestData.Json))
            .Content.ReadFromJsonAsync<ShiftResult>(TestData.Json))!.Shift;

        var assigned = await Read<PartnerBooking>(await s.Api.GetAsync($"/api/partner/v1/bookings/{booking.Reference}"));
        await staff.PutAsJsonAsync($"/api/admin/shifts/{shift.Id}/status", new ShiftStatusRequest { Status = AssignmentStatus.Confirmed }, TestData.Json);
        var confirmed = await Read<PartnerBooking>(await s.Api.GetAsync($"/api/partner/v1/bookings/{booking.Reference}"));

        Assert.Equal(BookingStatus.Assigned, assigned.Status);
        Assert.Null(assigned.Driver); // not before the driver accepts
        Assert.Equal("Giulia", confirmed.Driver?.FirstName);
        Assert.Equal("GB456DE", confirmed.Driver?.PlateNumber);
        // Booking.com asked us not to message its passengers.
        Assert.DoesNotContain(s.Factory.WhatsApp.Sent, m => m.Template == NotificationKind.ClientDriverDetails);

        var cancelled = await Read<PartnerBooking>(await s.Api.PostAsJsonAsync($"/api/partner/v1/bookings/{booking.Reference}/cancel",
            new PartnerCancelRequest { Reason = "Guest changed plans" }, TestData.Json));
        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
        Assert.Null(cancelled.Driver);
        var after = await staff.GetFromJsonAsync<ShiftDto>($"/api/admin/shifts/{shift.Id}", TestData.Json);
        Assert.Equal(AssignmentStatus.Cancelled, after!.Status); // its only ride was cancelled

        var detail = await staff.GetFromJsonAsync<BookingDetail>($"/api/admin/bookings/{id}", TestData.Json);
        Assert.Equal(BookingSource.Booking, detail!.Source);
        Assert.Equal("BKG-77", detail.ExternalReference);
    }

    [Fact]
    public async Task Status_changes_are_pushed_to_the_partner_webhook_signed_and_retried()
    {
        var s = await SetupAsync(new PartnerRequest
        {
            Name = "Agency", ContactEmail = "ops@agency.test", WebhookUrl = "http://localhost/hooks/magnus",
        });
        using var _ = s.Factory;
        var secret = (await s.Admin.GetFromJsonAsync<PartnerSecretResponse>($"/api/admin/partners/{s.Partner.Id}/webhook-secret", TestData.Json))!.Secret;

        s.Factory.WebhookReceiver.Answer = HttpStatusCode.InternalServerError;
        var booking = await Read<PartnerBooking>(await s.Api.PostAsJsonAsync("/api/partner/v1/bookings", Booking("AG-1"), TestData.Json), HttpStatusCode.Created);
        using (var scope = s.Factory.Services.CreateScope())
        {
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<IPartnerWebhookDispatcher>().DeliverDueAsync(CancellationToken.None));
        }
        var failing = await s.Admin.GetFromJsonAsync<List<WebhookDeliveryDto>>($"/api/admin/partners/{s.Partner.Id}/webhooks", TestData.Json);
        Assert.Equal("HTTP 500", Assert.Single(failing!).LastError);

        // The retry is not due yet; cancelling queues a second event, which is delivered now.
        s.Factory.WebhookReceiver.Answer = HttpStatusCode.OK;
        await s.Api.PostAsJsonAsync($"/api/partner/v1/bookings/{booking.Reference}/cancel", new PartnerCancelRequest { Reason = "Test" }, TestData.Json);
        using (var scope = s.Factory.Services.CreateScope())
        {
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IPartnerWebhookDispatcher>().DeliverDueAsync(CancellationToken.None));
        }

        var received = s.Factory.WebhookReceiver.Requests.Last();
        Assert.Equal("booking.updated", received.Event);
        Assert.Contains("\"status\":\"Cancelled\"", received.Body);
        Assert.Contains("\"externalReference\":\"AG-1\"", received.Body);
        // The partner can verify: v1 = HMAC-SHA256(secret, "{t}.{body}").
        var parts = received.Signature.Split(',').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => p[1]);
        var expected = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{parts["t"]}.{received.Body}")));
        Assert.Equal(expected, parts["v1"]);
    }

    [Fact]
    public async Task Monthly_statement_lists_rides_with_commission()
    {
        var s = await SetupAsync();
        using var _ = s.Factory;
        var pickup = TestData.QuietPickup();
        var first = await Read<PartnerBooking>(await s.Api.PostAsJsonAsync("/api/partner/v1/bookings",
            Booking("DAN-1", TestData.Trip() with { PickupAt = pickup }), TestData.Json), HttpStatusCode.Created);
        var second = await Read<PartnerBooking>(await s.Api.PostAsJsonAsync("/api/partner/v1/bookings",
            Booking("DAN-2", TestData.Trip() with { PickupAt = pickup.AddHours(3) }), TestData.Json), HttpStatusCode.Created);
        await s.Api.PostAsJsonAsync($"/api/partner/v1/bookings/{second.Reference}/cancel", new PartnerCancelRequest { Reason = "No show" }, TestData.Json);

        var csv = await s.Admin.GetStringAsync($"/api/admin/partners/{s.Partner.Id}/statement?year={pickup.Year}&month={pickup.Month}");

        var lines = csv.Trim().Split('\n');
        Assert.Equal(4, lines.Length); // header, two rides, total
        var it = System.Globalization.CultureInfo.GetCultureInfo("it-IT");
        var commission = Math.Round(first.TotalPrice * 0.05m, 2, MidpointRounding.AwayFromZero);
        Assert.StartsWith($"TOTAL;;;;;;;;{first.TotalPrice.ToString("0.00", it)};{commission.ToString("0.00", it)}", lines[^1]);
    }

    [Fact]
    public async Task Partner_api_description_is_public()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var document = await client.GetStringAsync("/api/partner/openapi/partner.json");
        var internalDocument = await client.GetAsync("/api/partner/openapi/internal.json");

        Assert.Contains("/api/partner/v1/bookings", document);
        Assert.DoesNotContain("/api/admin/", document);
        Assert.Equal(HttpStatusCode.NotFound, internalDocument.StatusCode);
    }
}
