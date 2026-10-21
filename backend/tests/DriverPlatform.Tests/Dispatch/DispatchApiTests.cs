using System.Net;
using System.Net.Http.Json;
using DriverPlatform.Application.Dispatch;
using DriverPlatform.Application.Geo;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Tests.Infrastructure;

namespace DriverPlatform.Tests.Dispatch;

public sealed class DispatchApiTests
{
    private static DriverRequest Driver(string phone, DateOnly? nccExpiry = null) => new()
    {
        FirstName = "Marco",
        LastName = "Bianchi " + phone[^2..],
        Phone = phone,
        Languages = ["it", "en"],
        Documents =
        [
            new DriverDocumentDto { Type = DriverDocumentType.DrivingLicence, ExpiresOn = Today().AddYears(3) },
            new DriverDocumentDto { Type = DriverDocumentType.NccLicence, ExpiresOn = nccExpiry ?? Today().AddYears(2) },
        ],
    };

    private static VehicleRequest Vehicle(string plate, VehicleCategory category, int seats, int luggage = 4) => new()
    {
        PlateNumber = plate,
        Make = "Mercedes-Benz",
        Model = category.ToString(),
        Category = category,
        Seats = seats,
        LuggageCapacity = luggage,
        ChildSeatsAvailable = 1,
        InsuranceExpiresOn = Today().AddYears(1),
        InspectionExpiresOn = Today().AddYears(1),
    };

    private static DateOnly Today() => DateOnly.FromDateTime(ItalyTime.ToLocal(DateTimeOffset.UtcNow));

    private static async Task<T> Read<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(TestData.Json))!;
    }

    private sealed record Fleet(HttpClient Staff, DriverDto Driver, DriverDto OtherDriver, VehicleDto Sedan, VehicleDto Van);

    private static async Task<Fleet> FleetAsync(ApiFactory factory)
    {
        var staff = await factory.CreateDispatcherClientAsync();
        var driver = await Read<DriverDto>(await staff.PostAsJsonAsync("/api/admin/drivers", Driver("+393400000001"), TestData.Json), HttpStatusCode.Created);
        var other = await Read<DriverDto>(await staff.PostAsJsonAsync("/api/admin/drivers", Driver("+393400000002"), TestData.Json), HttpStatusCode.Created);
        var sedan = await Read<VehicleDto>(await staff.PostAsJsonAsync("/api/admin/vehicles", Vehicle("ga 123 bc", VehicleCategory.Sedan, 3), TestData.Json), HttpStatusCode.Created);
        var van = await Read<VehicleDto>(await staff.PostAsJsonAsync("/api/admin/vehicles", Vehicle("GC789FG", VehicleCategory.Van, 7, 8), TestData.Json), HttpStatusCode.Created);
        return new Fleet(staff, driver, other, sedan, van);
    }

    private static Task<HttpResponseMessage> CreateShift(HttpClient staff, Guid driver, Guid vehicle, params Guid[] bookings) =>
        staff.PostAsJsonAsync("/api/admin/shifts", new ShiftRequest { DriverId = driver, VehicleId = vehicle, BookingIds = [.. bookings] }, TestData.Json);

    private static async Task<BookingListItem> FindBooking(HttpClient staff, string reference) =>
        (await staff.GetFromJsonAsync<PagedResult<BookingListItem>>($"/api/admin/bookings?search={reference}&includeUnpaid=true", TestData.Json))!.Items.Single();

    [Fact]
    public async Task Drivers_and_vehicles_are_kept_unique_and_normalised()
    {
        using var factory = new ApiFactory();
        var fleet = await FleetAsync(factory);

        var samePhone = await fleet.Staff.PostAsJsonAsync("/api/admin/drivers", Driver("+39 340 000 0001"), TestData.Json);
        var samePlate = await fleet.Staff.PostAsJsonAsync("/api/admin/vehicles", Vehicle("GA-123-BC", VehicleCategory.Sedan, 3), TestData.Json);

        Assert.Equal("GA123BC", fleet.Sedan.PlateNumber);
        Assert.Equal(2, fleet.Driver.Documents.Count);
        Assert.Equal(HttpStatusCode.BadRequest, samePhone.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, samePlate.StatusCode);
    }

    [Fact]
    public async Task Booking_list_hides_unpaid_bookings_and_finds_by_name()
    {
        using var factory = new ApiFactory();
        var staff = await factory.CreateDispatcherClientAsync();
        var client = factory.CreateClient();
        var paid = await client.BookAndPayAsync();
        await client.PostAsJsonAsync("/api/bookings", TestData.Booking(TestData.Trip()), TestData.Json); // never paid

        var list = await staff.GetFromJsonAsync<PagedResult<BookingListItem>>("/api/admin/bookings", TestData.Json);
        var unassigned = await staff.GetFromJsonAsync<PagedResult<BookingListItem>>("/api/admin/bookings?unassigned=true&search=rossi", TestData.Json);
        var withUnpaid = await staff.GetFromJsonAsync<PagedResult<BookingListItem>>("/api/admin/bookings?includeUnpaid=true", TestData.Json);

        Assert.Equal(paid.Reference, Assert.Single(list!.Items).Reference);
        Assert.Equal(BookingStatus.New, list.Items[0].Status);
        Assert.Single(unassigned!.Items);
        Assert.Equal(2, withUnpaid!.Total);
    }

    [Fact]
    public async Task Shift_assigns_the_bookings_and_cancelling_it_frees_them()
    {
        using var factory = new ApiFactory();
        var fleet = await FleetAsync(factory);
        var client = factory.CreateClient();
        var first = await client.BookAndPayAsync(TestData.Trip() with { PickupAt = TestData.QuietPickup().Date.AddHours(9) });
        var second = await client.BookAndPayAsync(TestData.Trip() with { PickupAt = TestData.QuietPickup().Date.AddHours(9).AddMinutes(30) });
        var firstId = (await FindBooking(fleet.Staff, first.Reference)).Id;
        var secondId = (await FindBooking(fleet.Staff, second.Reference)).Id;

        var result = await Read<ShiftResult>(await CreateShift(fleet.Staff, fleet.Driver.Id, fleet.Sedan.Id, secondId, firstId), HttpStatusCode.Created);

        Assert.Equal(AssignmentStatus.Draft, result.Shift.Status);
        Assert.Equal([first.Reference, second.Reference], result.Shift.Bookings.Select(b => b.Reference));
        Assert.All(result.Shift.Bookings, b => Assert.Equal(BookingStatus.Assigned, b.Status));
        Assert.Equal(DateOnly.FromDateTime(TestData.QuietPickup()), result.Shift.ShiftDate);
        // The two rides are close together: a warning, not an error.
        Assert.Contains(result.Warnings, w => w.Contains("min between"));
        Assert.Equal(fleet.Driver.FirstName + " " + fleet.Driver.LastName, (await FindBooking(fleet.Staff, first.Reference)).DriverName);

        var cancel = await fleet.Staff.PutAsJsonAsync($"/api/admin/shifts/{result.Shift.Id}/status",
            new ShiftStatusRequest { Status = AssignmentStatus.Cancelled, Reason = "Driver ill" }, TestData.Json);
        Assert.Equal(AssignmentStatus.Cancelled, (await Read<ShiftDto>(cancel)).Status);
        var freed = await FindBooking(fleet.Staff, first.Reference);
        Assert.Equal(BookingStatus.New, freed.Status);
        Assert.Null(freed.AssignmentId);
    }

    [Fact]
    public async Task Shift_rules_block_unpaid_bookings_small_cars_expired_documents_and_double_booking()
    {
        using var factory = new ApiFactory();
        var fleet = await FleetAsync(factory);
        var client = factory.CreateClient();
        var group = await client.BookAndPayAsync(TestData.Trip() with { Passengers = 6, LargeLuggage = 6 }, VehicleCategory.Van);
        var groupId = (await FindBooking(fleet.Staff, group.Reference)).Id;
        var unpaid = await client.PostAsJsonAsync("/api/bookings", TestData.Booking(TestData.Trip()), TestData.Json);
        var unpaidRef = (await unpaid.Content.ReadFromJsonAsync<Application.Bookings.CreateBookingResponse>(TestData.Json))!.Reference;
        var unpaidId = (await FindBooking(fleet.Staff, unpaidRef)).Id;
        var expired = await Read<DriverDto>(await fleet.Staff.PostAsJsonAsync("/api/admin/drivers",
            Driver("+393400000009", nccExpiry: Today().AddDays(-1)), TestData.Json), HttpStatusCode.Created);

        var tooSmall = await CreateShift(fleet.Staff, fleet.Driver.Id, fleet.Sedan.Id, groupId);
        var notPaid = await CreateShift(fleet.Staff, fleet.Driver.Id, fleet.Van.Id, unpaidId);
        var expiredDriver = await CreateShift(fleet.Staff, expired.Id, fleet.Van.Id, groupId);
        Assert.Equal(HttpStatusCode.BadRequest, tooSmall.StatusCode);
        Assert.Contains("seats", await tooSmall.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, notPaid.StatusCode);
        Assert.Contains("not paid", await notPaid.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, expiredDriver.StatusCode);
        Assert.Contains("NCC licence expired", await expiredDriver.Content.ReadAsStringAsync());

        // The van fits; then the same driver cannot take another ride at the same time.
        await Read<ShiftResult>(await CreateShift(fleet.Staff, fleet.Driver.Id, fleet.Van.Id, groupId), HttpStatusCode.Created);
        var overlapping = await client.BookAndPayAsync(TestData.Trip() with { PickupAt = TestData.QuietPickup().AddMinutes(20) });
        var overlappingId = (await FindBooking(fleet.Staff, overlapping.Reference)).Id;

        var busy = await CreateShift(fleet.Staff, fleet.Driver.Id, fleet.Sedan.Id, overlappingId);
        Assert.Equal(HttpStatusCode.BadRequest, busy.StatusCode);
        Assert.Contains("another shift", await busy.Content.ReadAsStringAsync());

        var availability = await fleet.Staff.GetFromJsonAsync<AvailabilityResponse>(
            $"/api/admin/shifts/availability?bookingIds={overlappingId}", TestData.Json);
        Assert.False(availability!.Drivers.Single(d => d.Id == fleet.Driver.Id).Available);
        Assert.True(availability.Drivers.Single(d => d.Id == fleet.OtherDriver.Id).Available);
        Assert.False(availability.Vehicles.Single(v => v.Id == fleet.Van.Id).Available);
        Assert.Equal(fleet.Sedan.Id, availability.Vehicles[0].Id); // smallest free car first
    }

    [Fact]
    public async Task Completing_every_booking_completes_the_shift()
    {
        using var factory = new ApiFactory();
        var fleet = await FleetAsync(factory);
        var booking = await factory.CreateClient().BookAndPayAsync();
        var id = (await FindBooking(fleet.Staff, booking.Reference)).Id;
        var shift = await Read<ShiftResult>(await CreateShift(fleet.Staff, fleet.Driver.Id, fleet.Sedan.Id, id), HttpStatusCode.Created);

        var backwards = await fleet.Staff.PutAsJsonAsync($"/api/admin/bookings/{id}/status", new BookingStatusRequest { Status = BookingStatus.New }, TestData.Json);
        var pickedUp = await fleet.Staff.PutAsJsonAsync($"/api/admin/bookings/{id}/status", new BookingStatusRequest { Status = BookingStatus.PickedUp }, TestData.Json);
        var completed = await fleet.Staff.PutAsJsonAsync($"/api/admin/bookings/{id}/status", new BookingStatusRequest { Status = BookingStatus.Completed }, TestData.Json);

        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
        Assert.Equal(BookingStatus.PickedUp, (await Read<BookingDetail>(pickedUp)).Status);
        Assert.Equal(BookingStatus.Completed, (await Read<BookingDetail>(completed)).Status);
        var after = await fleet.Staff.GetFromJsonAsync<ShiftDto>($"/api/admin/shifts/{shift.Shift.Id}", TestData.Json);
        Assert.Equal(AssignmentStatus.Completed, after!.Status);
    }

    [Fact]
    public async Task Removing_a_booking_from_a_shift_puts_it_back_in_the_queue()
    {
        using var factory = new ApiFactory();
        var fleet = await FleetAsync(factory);
        var client = factory.CreateClient();
        var a = await client.BookAndPayAsync(TestData.Trip() with { PickupAt = TestData.QuietPickup().Date.AddHours(8) });
        var b = await client.BookAndPayAsync(TestData.Trip() with { PickupAt = TestData.QuietPickup().Date.AddHours(12) });
        var aId = (await FindBooking(fleet.Staff, a.Reference)).Id;
        var bId = (await FindBooking(fleet.Staff, b.Reference)).Id;
        var shift = await Read<ShiftResult>(await CreateShift(fleet.Staff, fleet.Driver.Id, fleet.Sedan.Id, aId, bId), HttpStatusCode.Created);
        await fleet.Staff.PutAsJsonAsync($"/api/admin/shifts/{shift.Shift.Id}/status", new ShiftStatusRequest { Status = AssignmentStatus.Confirmed }, TestData.Json);

        var updated = await Read<ShiftResult>(await fleet.Staff.PutAsJsonAsync($"/api/admin/shifts/{shift.Shift.Id}",
            new ShiftRequest { DriverId = fleet.Driver.Id, VehicleId = fleet.Sedan.Id, BookingIds = [aId] }, TestData.Json));

        Assert.Equal(a.Reference, Assert.Single(updated.Shift.Bookings).Reference);
        Assert.Equal(AssignmentStatus.Draft, updated.Shift.Status); // changed, so it needs confirming again
        Assert.Equal(BookingStatus.New, (await FindBooking(fleet.Staff, b.Reference)).Status);
    }

    [Fact]
    public async Task Dashboard_counts_rides_to_assign_and_expiring_documents()
    {
        using var factory = new ApiFactory();
        var staff = await factory.CreateDispatcherClientAsync();
        await staff.PostAsJsonAsync("/api/admin/drivers", Driver("+393400000005", nccExpiry: Today().AddDays(10)), TestData.Json);
        var soon = TestData.Trip() with { PickupAt = ItalyTime.ToLocal(DateTimeOffset.UtcNow).AddHours(5) };
        await factory.CreateClient().BookAndPayAsync(soon);

        var dashboard = await staff.GetFromJsonAsync<DashboardResponse>("/api/admin/dashboard", TestData.Json);

        Assert.Equal(1, dashboard!.UnassignedNext48h);
        Assert.Single(dashboard.NextUnassigned);
        var alert = Assert.Single(dashboard.DocumentAlerts);
        Assert.Equal("NCC licence", alert.Document);
        Assert.False(alert.Expired);
    }

    [Fact]
    public async Task Dispatch_needs_a_signed_in_staff_member()
    {
        using var factory = new ApiFactory();

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/admin/bookings")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/admin/drivers")).StatusCode);
    }
}
