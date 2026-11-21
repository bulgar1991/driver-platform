using System.Net;
using System.Net.Http.Json;
using DriverPlatform.Application.Dispatch;
using DriverPlatform.Application.Geo;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Tests.Infrastructure;

namespace DriverPlatform.Tests.Dispatch;

public sealed class CalendarTests
{
    private static DateOnly Today() => DateOnly.FromDateTime(OfficeTime.ToLocal(DateTimeOffset.UtcNow));

    [Fact]
    public async Task Calendar_shows_the_rides_of_the_period_with_driver_and_car()
    {
        using var factory = new ApiFactory();
        var staff = await factory.CreateDispatcherClientAsync();
        var driver = (await (await staff.PostAsJsonAsync("/api/admin/drivers", new DriverRequest
        {
            FirstName = "Anna",
            LastName = "Keller",
            Phone = "+41790000001",
            Documents =
            [
                new DriverDocumentDto { Type = DriverDocumentType.DrivingLicence, ExpiresOn = Today().AddYears(3) },
                new DriverDocumentDto { Type = DriverDocumentType.NccLicence, ExpiresOn = Today().AddYears(2) },
            ],
        }, TestData.Json)).Content.ReadFromJsonAsync<DriverDto>(TestData.Json))!;
        var car = (await (await staff.PostAsJsonAsync("/api/admin/vehicles", new VehicleRequest
        {
            PlateNumber = "ZH12345",
            Make = "Mercedes-Benz",
            Model = "E",
            Category = VehicleCategory.Sedan,
            Seats = 3,
            LuggageCapacity = 4,
            InsuranceExpiresOn = Today().AddYears(1),
            InspectionExpiresOn = Today().AddYears(1),
        }, TestData.Json)).Content.ReadFromJsonAsync<VehicleDto>(TestData.Json))!;
        var client = factory.CreateClient();
        var assigned = await client.BookAndPayAsync();
        var waiting = await client.BookAndPayAsync(TestData.Trip() with { PickupAt = TestData.QuietPickup().AddHours(3) });
        await client.PostAsJsonAsync("/api/bookings", TestData.Booking(TestData.Trip()), TestData.Json); // never paid
        var assignedId = (await staff.GetFromJsonAsync<PagedResult<BookingListItem>>(
            $"/api/admin/bookings?search={assigned.Reference}", TestData.Json))!.Items.Single().Id;
        var shift = await staff.PostAsJsonAsync("/api/admin/shifts",
            new ShiftRequest { DriverId = driver.Id, VehicleId = car.Id, BookingIds = [assignedId] }, TestData.Json);
        Assert.Equal(HttpStatusCode.Created, shift.StatusCode);

        var day = DateOnly.FromDateTime(TestData.QuietPickup());
        var calendar = await staff.GetFromJsonAsync<CalendarResponse>(
            $"/api/admin/calendar?from={day:yyyy-MM-dd}&to={day.AddDays(1):yyyy-MM-dd}", TestData.Json);

        Assert.Equal(2, calendar!.Rides.Count); // not the unpaid one
        var ride = calendar.Rides.Single(r => r.Reference == assigned.Reference);
        Assert.Equal(driver.Id, ride.DriverId);
        Assert.Equal(car.Id, ride.VehicleId);
        Assert.Equal("ZH12345", ride.VehiclePlate);
        Assert.Equal(AssignmentStatus.Draft, ride.ShiftStatus);
        Assert.Null(calendar.Rides.Single(r => r.Reference == waiting.Reference).DriverId);
        Assert.Contains(calendar.Vehicles, v => v.Id == car.Id);
        Assert.Contains(calendar.Drivers, d => d.Id == driver.Id);
        Assert.All(calendar.Rides, r => Assert.Null(r.DriverPay)); // dispatchers do not see pay

        var tooLong = await staff.GetAsync($"/api/admin/calendar?from={day:yyyy-MM-dd}&to={day.AddDays(80):yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }
}
