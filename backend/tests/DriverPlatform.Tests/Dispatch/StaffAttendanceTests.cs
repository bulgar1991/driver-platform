using System.Net;
using System.Net.Http.Json;
using DriverPlatform.Application.Attendance;
using DriverPlatform.Application.Dispatch;
using DriverPlatform.Application.Geo;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Tests.Infrastructure;

namespace DriverPlatform.Tests.Dispatch;

public sealed class StaffAttendanceTests
{
    private static DateOnly Today() => DateOnly.FromDateTime(OfficeTime.ToLocal(DateTimeOffset.UtcNow));

    private static DriverRequest Driver(string phone) => new()
    {
        FirstName = "Luca",
        LastName = "Verdi " + phone[^2..],
        Phone = phone,
        Documents =
        [
            new DriverDocumentDto { Type = DriverDocumentType.DrivingLicence, ExpiresOn = Today().AddYears(3) },
            new DriverDocumentDto { Type = DriverDocumentType.NccLicence, ExpiresOn = Today().AddYears(2) },
        ],
    };

    private static VehicleRequest Vehicle(string plate) => new()
    {
        PlateNumber = plate,
        Make = "Mercedes-Benz",
        Model = "E",
        Category = VehicleCategory.Sedan,
        Seats = 3,
        LuggageCapacity = 4,
        InsuranceExpiresOn = Today().AddYears(1),
        InspectionExpiresOn = Today().AddYears(1),
    };

    private static async Task<T> Read<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(TestData.Json))!;
    }

    [Fact]
    public async Task Staff_card_keeps_the_personnel_details()
    {
        using var factory = new ApiFactory();
        var staff = await factory.CreateDispatcherClientAsync();
        var request = Driver("+393400000011") with
        {
            EmployeeNumber = "E-17",
            TaxCode = "vrdlcu80a01h501u",
            DateOfBirth = new DateOnly(1980, 1, 1),
            PlaceOfBirth = "Roma",
            HiredOn = new DateOnly(2024, 3, 1),
            ContractType = DriverContractType.FixedTerm,
            MaxDrivingHours = 9,
            HourlyCost = 32.5m,
            JobFunction = "Chauffeur",
            Address = "Via Roma 1",
            HomePhone = "+39 06 1234567",
            NotifyBy = DriverNotify.Email,
        };

        var driver = await Read<DriverDto>(await staff.PostAsJsonAsync("/api/admin/drivers", request, TestData.Json), HttpStatusCode.Created);
        var backwards = await staff.PutAsJsonAsync($"/api/admin/drivers/{driver.Id}",
            request with { LeftOn = new DateOnly(2024, 2, 1) }, TestData.Json);

        Assert.Equal("E-17", driver.EmployeeNumber);
        Assert.Equal("VRDLCU80A01H501U", driver.TaxCode);
        Assert.Equal(DriverContractType.FixedTerm, driver.ContractType);
        Assert.Equal(32.5m, driver.HourlyCost);
        Assert.Equal(DriverNotify.Email, driver.NotifyBy);
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
    }

    [Fact]
    public async Task Holiday_in_the_calendar_blocks_shifts_that_day_but_a_few_hours_do_not()
    {
        using var factory = new ApiFactory();
        var staff = await factory.CreateDispatcherClientAsync();
        var driver = await Read<DriverDto>(await staff.PostAsJsonAsync("/api/admin/drivers", Driver("+393400000012"), TestData.Json), HttpStatusCode.Created);
        var other = await Read<DriverDto>(await staff.PostAsJsonAsync("/api/admin/drivers", Driver("+393400000013"), TestData.Json), HttpStatusCode.Created);
        var car = await Read<VehicleDto>(await staff.PostAsJsonAsync("/api/admin/vehicles", Vehicle("AT123AA"), TestData.Json), HttpStatusCode.Created);
        var booking = await factory.CreateClient().BookAndPayAsync();
        var bookingId = (await staff.GetFromJsonAsync<PagedResult<BookingListItem>>(
            $"/api/admin/bookings?search={booking.Reference}", TestData.Json))!.Items.Single().Id;
        var day = DateOnly.FromDateTime(TestData.QuietPickup());

        var calendar = await staff.GetFromJsonAsync<AttendanceResponse>($"/api/admin/attendance?from={day.AddDays(-3):yyyy-MM-dd}&to={day.AddDays(3):yyyy-MM-dd}", TestData.Json);
        var holiday = calendar!.Types.First(t => t.MakesUnavailable);
        var onCall = calendar.Types.First(t => !t.MakesUnavailable);
        Assert.Contains(calendar.People, p => p.Id == driver.Id);

        // A week of holiday without the weekend, then two hours of something else for the other driver.
        var week = await Read<List<AttendanceEntryDto>>(await staff.PutAsJsonAsync("/api/admin/attendance", new AttendanceRequest
        {
            DriverId = driver.Id, TypeId = holiday.Id, From = day.AddDays(-3), To = day.AddDays(3), SkipWeekends = true,
        }, TestData.Json));
        Assert.Equal(5, week.Count);
        Assert.DoesNotContain(week, e => e.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        await Read<List<AttendanceEntryDto>>(await staff.PutAsJsonAsync("/api/admin/attendance", new AttendanceRequest
        {
            DriverId = other.Id, TypeId = holiday.Id, From = day, To = day, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(10, 0),
        }, TestData.Json));
        var halfTimes = await staff.PutAsJsonAsync("/api/admin/attendance", new AttendanceRequest
        {
            DriverId = other.Id, TypeId = onCall.Id, From = day, To = day, StartTime = new TimeOnly(8, 0),
        }, TestData.Json);
        Assert.Equal(HttpStatusCode.BadRequest, halfTimes.StatusCode);

        var availability = await staff.GetFromJsonAsync<AvailabilityResponse>($"/api/admin/shifts/availability?bookingIds={bookingId}", TestData.Json);
        var blocked = availability!.Drivers.Single(d => d.Id == driver.Id);
        var weekday = day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
        Assert.Equal(!weekday, blocked.Available);
        Assert.True(availability.Drivers.Single(d => d.Id == other.Id).Available);
        if (weekday)
        {
            Assert.Contains(blocked.Problems, p => p.Contains(holiday.Name));
            var shift = await staff.PostAsJsonAsync("/api/admin/shifts",
                new ShiftRequest { DriverId = driver.Id, VehicleId = car.Id, BookingIds = [bookingId] }, TestData.Json);
            Assert.Equal(HttpStatusCode.BadRequest, shift.StatusCode);
        }

        // Clearing the days frees the driver.
        var cleared = await Read<int>(await staff.PostAsJsonAsync("/api/admin/attendance/clear",
            new AttendanceClearRequest { DriverId = driver.Id, From = day.AddDays(-3), To = day.AddDays(3) }, TestData.Json));
        Assert.Equal(5, cleared);
        availability = await staff.GetFromJsonAsync<AvailabilityResponse>($"/api/admin/shifts/availability?bookingIds={bookingId}", TestData.Json);
        Assert.True(availability!.Drivers.Single(d => d.Id == driver.Id).Available);

        // Kinds of event are for admins.
        var create = await staff.PostAsJsonAsync("/api/admin/attendance/types",
            new AttendanceTypeRequest { Name = "Strike", Code = "ST", Color = "#000000" }, TestData.Json);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task Admin_manages_the_kinds_of_event_but_cannot_delete_one_in_use()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();
        var driver = await Read<DriverDto>(await admin.PostAsJsonAsync("/api/admin/drivers", Driver("+393400000014"), TestData.Json), HttpStatusCode.Created);

        var kind = await Read<AttendanceTypeDto>(await admin.PostAsJsonAsync("/api/admin/attendance/types",
            new AttendanceTypeRequest { Name = "Strike", Code = "st", Color = "#AA0000", MakesUnavailable = true }, TestData.Json), HttpStatusCode.Created);
        Assert.Equal("ST", kind.Code);
        Assert.Equal("#aa0000", kind.Color);
        await Read<List<AttendanceEntryDto>>(await admin.PutAsJsonAsync("/api/admin/attendance", new AttendanceRequest
        {
            DriverId = driver.Id, TypeId = kind.Id, From = Today(), To = Today(),
        }, TestData.Json));

        var inUse = await admin.DeleteAsync($"/api/admin/attendance/types/{kind.Id}");
        Assert.Equal(HttpStatusCode.Conflict, inUse.StatusCode);

        var unused = await Read<AttendanceTypeDto>(await admin.PostAsJsonAsync("/api/admin/attendance/types",
            new AttendanceTypeRequest { Name = "Unused", Code = "U", Color = "#00aa00" }, TestData.Json), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/attendance/types/{unused.Id}")).StatusCode);
        var tooLong = await admin.GetAsync($"/api/admin/attendance?from=2025-01-01&to=2026-12-31");
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task Vehicle_keeps_its_details_deadlines_and_refuellings()
    {
        using var factory = new ApiFactory();
        var staff = await factory.CreateDispatcherClientAsync();
        var driver = await Read<DriverDto>(await staff.PostAsJsonAsync("/api/admin/drivers", Driver("+393400000015"), TestData.Json), HttpStatusCode.Created);
        var car = await Read<VehicleDto>(await staff.PostAsJsonAsync("/api/admin/vehicles", Vehicle("AT456AA") with
        {
            Code = "T1",
            ChassisNumber = "wdd1234567890",
            CostPerKm = 0.45m,
            DefaultDriverId = driver.Id,
            WheelchairRamp = true,
            HasWifi = true,
            MileageKm = 10_000,
        }, TestData.Json), HttpStatusCode.Created);

        Assert.Equal("T1", car.Code);
        Assert.Equal("WDD1234567890", car.ChassisNumber);
        Assert.Equal(driver.FirstName + " " + driver.LastName, car.DefaultDriverName);
        Assert.True(car.WheelchairRamp);
        Assert.Equal(0, car.DocumentAlerts);

        var noDue = await staff.PostAsJsonAsync($"/api/admin/vehicles/{car.Id}/expirations",
            new VehicleExpirationRequest { Title = "Road tax" }, TestData.Json);
        Assert.Equal(HttpStatusCode.BadRequest, noDue.StatusCode);
        await Read<VehicleExpirationDto>(await staff.PostAsJsonAsync($"/api/admin/vehicles/{car.Id}/expirations",
            new VehicleExpirationRequest { Title = "Road tax", DueOn = Today().AddDays(10) }, TestData.Json), HttpStatusCode.Created);
        await Read<VehicleExpirationDto>(await staff.PostAsJsonAsync($"/api/admin/vehicles/{car.Id}/expirations",
            new VehicleExpirationRequest { Title = "Service", DueKm = 40_000 }, TestData.Json), HttpStatusCode.Created);

        foreach (var (km, liters) in new[] { (10_500, 40m), (11_000, 30m), (11_500, 35m) })
        {
            await Read<VehicleRefuelDto>(await staff.PostAsJsonAsync($"/api/admin/vehicles/{car.Id}/refuels",
                new VehicleRefuelRequest { Date = Today(), OdometerKm = km, Liters = liters, Cost = liters * 1.8m, DriverId = driver.Id },
                TestData.Json), HttpStatusCode.Created);
        }

        var log = await staff.GetFromJsonAsync<VehicleLogResponse>($"/api/admin/vehicles/{car.Id}/log", TestData.Json);
        Assert.Equal(2, log!.Expirations.Count);
        Assert.Equal("Road tax", log.Expirations[0].Title); // dated first
        Assert.Equal(3, log.Refuels.Count);
        Assert.Equal(1000, log.DistanceKm);
        Assert.Equal(6.5m, log.AverageConsumption); // (30 + 35) l over 1,000 km
        Assert.Equal(11_500, log.MileageKm);
        var list = await staff.GetFromJsonAsync<List<VehicleDto>>("/api/admin/vehicles", TestData.Json);
        Assert.Equal(1, list!.Single(v => v.Id == car.Id).DocumentAlerts); // the road tax, not the far service

        Assert.Equal(HttpStatusCode.NoContent,
            (await staff.DeleteAsync($"/api/admin/vehicles/{car.Id}/refuels/{log.Refuels[0].Id}")).StatusCode);
    }
}
