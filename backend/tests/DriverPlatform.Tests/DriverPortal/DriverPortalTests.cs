using System.Net;
using System.Net.Http.Json;
using DriverPlatform.Application.Dispatch;
using DriverPlatform.Application.DriverPortal;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Reviews;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Infrastructure.Persistence;
using DriverPlatform.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DriverPlatform.Tests.DriverPortal;

public sealed class DriverPortalTests
{
    private const string DriverPhone = "+393400000001";

    private sealed record Setup(ApiFactory Factory, HttpClient Staff, HttpClient Public, DriverDto Driver, ShiftDto Shift, Guid BookingId, string DriverToken);

    private static DateOnly Today() => DateOnly.FromDateTime(ItalyTime.ToLocal(DateTimeOffset.UtcNow));

    /// <summary>A paid booking in a draft shift for a driver with valid documents.</summary>
    private static async Task<Setup> SetupAsync(string? clientEmail = "maria@example.com")
    {
        var factory = new ApiFactory();
        var staff = await factory.CreateDispatcherClientAsync();
        var client = factory.CreateClient();

        var driver = (await (await staff.PostAsJsonAsync("/api/admin/drivers", new DriverRequest
        {
            FirstName = "Marco",
            LastName = "Bianchi",
            Phone = DriverPhone,
            Email = "marco@drivers.test",
            Documents =
            [
                new DriverDocumentDto { Type = DriverDocumentType.DrivingLicence, ExpiresOn = Today().AddYears(3) },
                new DriverDocumentDto { Type = DriverDocumentType.NccLicence, ExpiresOn = Today().AddYears(3) },
            ],
        }, TestData.Json)).Content.ReadFromJsonAsync<DriverDto>(TestData.Json))!;
        var vehicle = (await (await staff.PostAsJsonAsync("/api/admin/vehicles", new VehicleRequest
        {
            PlateNumber = "GA123BC",
            Make = "Mercedes-Benz",
            Model = "E-Class",
            Color = "Black",
            Category = VehicleCategory.Sedan,
            Seats = 3,
            LuggageCapacity = 3,
            InsuranceExpiresOn = Today().AddYears(1),
            InspectionExpiresOn = Today().AddYears(1),
        }, TestData.Json)).Content.ReadFromJsonAsync<VehicleDto>(TestData.Json))!;

        // Soon enough for the driver's pickup window (3 hours), late enough for the booking lead time (2 hours).
        var pickupAt = ItalyTime.ToLocal(DateTimeOffset.UtcNow).AddHours(2.5);
        var request = TestData.Booking(TestData.Trip(TestData.VeniceRialto, TestData.VeniceAirport) with
        {
            PickupAt = pickupAt.AddTicks(-(pickupAt.Ticks % TimeSpan.TicksPerMinute)),
        });
        var booked = (await (await client.PostAsJsonAsync("/api/bookings", request, TestData.Json))
            .Content.ReadFromJsonAsync<Application.Bookings.CreateBookingResponse>(TestData.Json))!;
        (await client.PostAsJsonAsync($"/api/dev/payments/{booked.PaymentUrl!.Split('/').Last()}/complete", new { method = "Card" }, TestData.Json)).EnsureSuccessStatusCode();

        Guid bookingId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var booking = await db.Bookings.Include(b => b.Client).SingleAsync(b => b.Reference == booked.Reference);
            bookingId = booking.Id;
            // Website bookings always have an email; partner bookings (step 6) may not.
            if (clientEmail is null)
            {
                booking.Client.Email = null;
                await db.SaveChangesAsync();
            }
        }

        var shift = (await (await staff.PostAsJsonAsync("/api/admin/shifts",
            new ShiftRequest { DriverId = driver.Id, VehicleId = vehicle.Id, BookingIds = [bookingId] }, TestData.Json))
            .Content.ReadFromJsonAsync<ShiftResult>(TestData.Json))!.Shift;
        var link = (await staff.GetFromJsonAsync<DriverLinkResponse>($"/api/admin/drivers/{driver.Id}/link", TestData.Json))!;
        factory.Emails.Sent.Clear();
        return new Setup(factory, staff, client, driver, shift, bookingId, link.Url.Split('/').Last());
    }

    private static Task<HttpResponseMessage> SetShiftStatus(Setup s, AssignmentStatus status, string? reason = null) =>
        s.Staff.PutAsJsonAsync($"/api/admin/shifts/{s.Shift.Id}/status", new ShiftStatusRequest { Status = status, Reason = reason }, TestData.Json);

    [Fact]
    public async Task Draft_shifts_are_not_on_the_driver_link_until_sent()
    {
        var s = await SetupAsync();
        using var _ = s.Factory;

        var before = await s.Public.GetFromJsonAsync<DriverPortalResponse>($"/api/driver/{s.DriverToken}", TestData.Json);
        var sent = await SetShiftStatus(s, AssignmentStatus.Sent);
        var after = await s.Public.GetFromJsonAsync<DriverPortalResponse>($"/api/driver/{s.DriverToken}", TestData.Json);

        Assert.Empty(before!.Shifts);
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        var shift = Assert.Single(after!.Shifts);
        Assert.True(shift.CanConfirm);
        var ride = Assert.Single(shift.Bookings);
        Assert.Equal("Piazzale Roma", ride.PickupMeetingPoint?.Name);
        Assert.Equal(TestData.VeniceRialto.Address, ride.Pickup.Address); // the client's exact address too
        Assert.Equal("Maria Rossi", ride.ClientName);

        // WhatsApp and email to the driver, both carrying the personal link.
        var whatsApp = Assert.Single(s.Factory.WhatsApp.Sent);
        Assert.Equal(NotificationKind.DriverShift, whatsApp.Template);
        Assert.Equal(DriverPhone, whatsApp.Phone);
        Assert.EndsWith(s.DriverToken, whatsApp.Parameters[^1]);
        Assert.Contains(s.Factory.Emails.Sent, e => e.To == "marco@drivers.test" && e.TextBody.Contains(s.DriverToken));

        // Opening the link marks the shift as seen.
        var dto = await s.Staff.GetFromJsonAsync<ShiftDto>($"/api/admin/shifts/{s.Shift.Id}", TestData.Json);
        Assert.Equal(AssignmentStatus.Seen, dto!.Status);
        Assert.Contains(dto.Messages, m => m.Channel == NotificationChannel.WhatsApp && m.Status == NotificationStatus.Sent);
    }

    [Fact]
    public async Task Driver_confirms_picks_up_and_completes_then_the_client_is_asked_for_a_review()
    {
        var s = await SetupAsync();
        using var _ = s.Factory;
        await SetShiftStatus(s, AssignmentStatus.Sent);
        var api = $"/api/driver/{s.DriverToken}";

        var tooEarly = await s.Public.PostAsync($"{api}/bookings/{s.BookingId}/picked-up", null);
        var confirmed = await s.Public.PostAsync($"{api}/shifts/{s.Shift.Id}/confirm", null);
        var skipPickup = await s.Public.PostAsync($"{api}/bookings/{s.BookingId}/completed", null);
        var pickedUp = await s.Public.PostAsync($"{api}/bookings/{s.BookingId}/picked-up", null);
        var completed = await s.Public.PostAsync($"{api}/bookings/{s.BookingId}/completed", null);

        Assert.Equal(HttpStatusCode.Conflict, tooEarly.StatusCode);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, skipPickup.StatusCode);
        Assert.Equal(HttpStatusCode.OK, pickedUp.StatusCode);
        var final = (await completed.Content.ReadFromJsonAsync<DriverPortalResponse>(TestData.Json))!;
        Assert.Equal(AssignmentStatus.Completed, final.Shifts.Single().Status);
        Assert.Equal(BookingStatus.Completed, final.Shifts.Single().Bookings.Single().Status);

        // On confirmation the client gets the driver's name, phone and car, by WhatsApp and email.
        var details = Assert.Single(s.Factory.WhatsApp.Sent, m => m.Template == NotificationKind.ClientDriverDetails);
        Assert.Contains("GA123BC", details.Parameters[^1]);
        Assert.Contains(s.Factory.Emails.Sent, e => e.To == "maria@example.com" && e.TextBody.Contains("Marco (+393400000001)"));

        // After drop-off: a one-time review link by email.
        var reviewEmail = Assert.Single(s.Factory.Emails.Sent, e => e.Subject.StartsWith("How was your ride"));
        var reviewToken = reviewEmail.TextBody.Split("/review/")[1].Trim();
        var page = await s.Public.GetFromJsonAsync<ReviewPage>($"/api/reviews/{reviewToken}", TestData.Json);
        Assert.Equal("Marco", page!.DriverFirstName);
        Assert.False(page.Submitted);

        var submit = await s.Public.PostAsJsonAsync($"/api/reviews/{reviewToken}", new SubmitReviewRequest { Rating = 5, Comment = "Perfect, on time." }, TestData.Json);
        var again = await s.Public.PostAsJsonAsync($"/api/reviews/{reviewToken}", new SubmitReviewRequest { Rating = 1 }, TestData.Json);
        Assert.Equal(HttpStatusCode.NoContent, submit.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var drivers = await s.Staff.GetFromJsonAsync<List<DriverDto>>("/api/admin/drivers", TestData.Json);
        var marco = drivers!.Single(d => d.Id == s.Driver.Id);
        Assert.Equal(5m, marco.Rating);
        Assert.Equal(1, marco.CompletedRides);
        var reviews = await s.Staff.GetFromJsonAsync<List<ReviewDto>>("/api/admin/reviews", TestData.Json);
        Assert.Equal("Perfect, on time.", Assert.Single(reviews!).Comment);

        // The audit log names the driver, not "system" or whoever else is signed in.
        using var scope = s.Factory.Services.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs;
        Assert.True(await audit.AnyAsync(a => a.Action == "shift.confirmed_by_driver" && a.Actor == "driver Marco Bianchi"));
        Assert.True(await audit.AnyAsync(a => a.Action == "auth.login" && a.Actor == "dispatcher@test.local"));
    }

    [Fact]
    public async Task Pickup_cannot_be_marked_days_before_the_ride()
    {
        var s = await SetupAsync();
        using var _ = s.Factory;
        using (var scope = s.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var booking = await db.Bookings.SingleAsync(b => b.Id == s.BookingId);
            booking.PickupAt = booking.PickupAt.AddDays(2);
            await db.SaveChangesAsync();
        }
        await SetShiftStatus(s, AssignmentStatus.Sent);
        await s.Public.PostAsync($"/api/driver/{s.DriverToken}/shifts/{s.Shift.Id}/confirm", null);

        var portal = await s.Public.GetFromJsonAsync<DriverPortalResponse>($"/api/driver/{s.DriverToken}", TestData.Json);
        var early = await s.Public.PostAsync($"/api/driver/{s.DriverToken}/bookings/{s.BookingId}/picked-up", null);

        Assert.False(portal!.Shifts.Single().Bookings.Single().CanPickUp);
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
    }

    [Fact]
    public async Task Client_without_email_gets_the_review_link_on_whatsapp()
    {
        var s = await SetupAsync(clientEmail: null);
        using var _ = s.Factory;
        await SetShiftStatus(s, AssignmentStatus.Confirmed);

        var completed = await s.Staff.PutAsJsonAsync($"/api/admin/bookings/{s.BookingId}/status",
            new BookingStatusRequest { Status = BookingStatus.Completed }, TestData.Json);

        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var review = Assert.Single(s.Factory.WhatsApp.Sent, m => m.Template == NotificationKind.ReviewRequest);
        Assert.Contains("/review/", review.Parameters[^1]);
    }

    [Fact]
    public async Task Changing_a_sent_shift_sends_the_update_and_asks_for_a_new_confirmation()
    {
        var s = await SetupAsync();
        using var _ = s.Factory;
        await SetShiftStatus(s, AssignmentStatus.Sent);
        await s.Public.PostAsync($"/api/driver/{s.DriverToken}/shifts/{s.Shift.Id}/confirm", null);

        var updated = await s.Staff.PutAsJsonAsync($"/api/admin/shifts/{s.Shift.Id}",
            new ShiftRequest { DriverId = s.Driver.Id, VehicleId = s.Shift.VehicleId, BookingIds = [s.BookingId], Notes = "Meet at gate B" }, TestData.Json);

        var result = (await updated.Content.ReadFromJsonAsync<ShiftResult>(TestData.Json))!;
        Assert.Equal(AssignmentStatus.Sent, result.Shift.Status);
        Assert.Contains(s.Factory.WhatsApp.Sent, m => m.Template == NotificationKind.DriverShiftChanged);
        var portal = await s.Public.GetFromJsonAsync<DriverPortalResponse>($"/api/driver/{s.DriverToken}", TestData.Json);
        Assert.True(portal!.Shifts.Single().CanConfirm);
        Assert.Equal("Meet at gate B", portal.Shifts.Single().Notes);
    }

    [Fact]
    public async Task Regenerated_link_replaces_the_old_one()
    {
        var s = await SetupAsync();
        using var _ = s.Factory;

        var rotated = (await (await s.Staff.PostAsync($"/api/admin/drivers/{s.Driver.Id}/link", null))
            .Content.ReadFromJsonAsync<DriverLinkResponse>(TestData.Json))!;

        Assert.Equal(HttpStatusCode.NotFound, (await s.Public.GetAsync($"/api/driver/{s.DriverToken}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Public.GetAsync($"/api/driver/{rotated.Url.Split('/').Last()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Public.GetAsync("/api/driver/made-up-token")).StatusCode);
    }

    [Fact]
    public async Task A_failed_whatsapp_message_is_logged_and_does_not_block_the_dispatcher()
    {
        var s = await SetupAsync();
        using var _ = s.Factory;
        s.Factory.WhatsApp.Failing.Add(DriverPhone);

        var sent = await SetShiftStatus(s, AssignmentStatus.Sent);

        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        var shift = (await sent.Content.ReadFromJsonAsync<ShiftDto>(TestData.Json))!;
        var whatsApp = shift.Messages.Single(m => m.Channel == NotificationChannel.WhatsApp);
        Assert.Equal(NotificationStatus.Failed, whatsApp.Status);
        Assert.Contains("not on WhatsApp", whatsApp.Error);
        Assert.Equal(NotificationStatus.Sent, shift.Messages.Single(m => m.Channel == NotificationChannel.Email).Status);
    }
}
