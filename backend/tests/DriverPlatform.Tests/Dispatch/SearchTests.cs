using System.Net;
using System.Net.Http.Json;
using DriverPlatform.Application.Dispatch;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Search;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Tests.Infrastructure;

namespace DriverPlatform.Tests.Dispatch;

public sealed class SearchTests
{
    private static DateOnly Today() => DateOnly.FromDateTime(OfficeTime.ToLocal(DateTimeOffset.UtcNow));

    [Fact]
    public async Task Search_filters_rides_offers_the_matching_reports_and_prints_them()
    {
        using var factory = new ApiFactory();
        var staff = await factory.CreateDispatcherClientAsync();
        var driver = (await (await staff.PostAsJsonAsync("/api/admin/drivers", new DriverRequest
        {
            FirstName = "Lena",
            LastName = "Huber",
            Phone = "+41790000002",
            Documents =
            [
                new DriverDocumentDto { Type = DriverDocumentType.DrivingLicence, ExpiresOn = Today().AddYears(3) },
                new DriverDocumentDto { Type = DriverDocumentType.NccLicence, ExpiresOn = Today().AddYears(2) },
            ],
        }, TestData.Json)).Content.ReadFromJsonAsync<DriverDto>(TestData.Json))!;
        var car = (await (await staff.PostAsJsonAsync("/api/admin/vehicles", new VehicleRequest
        {
            PlateNumber = "ZH54321",
            Make = "Mercedes-Benz",
            Model = "E",
            Category = VehicleCategory.Sedan,
            Seats = 3,
            LuggageCapacity = 4,
            InsuranceExpiresOn = Today().AddYears(1),
            InspectionExpiresOn = Today().AddYears(1),
        }, TestData.Json)).Content.ReadFromJsonAsync<VehicleDto>(TestData.Json))!;
        var client = factory.CreateClient();
        var first = await client.BookAndPayAsync();
        var second = await client.BookAndPayAsync(TestData.Trip() with { PickupAt = TestData.QuietPickup().AddHours(4) });
        var firstId = (await staff.GetFromJsonAsync<PagedResult<BookingListItem>>(
            $"/api/admin/bookings?search={first.Reference}", TestData.Json))!.Items.Single().Id;
        Assert.Equal(HttpStatusCode.Created, (await staff.PostAsJsonAsync("/api/admin/shifts",
            new ShiftRequest { DriverId = driver.Id, VehicleId = car.Id, BookingIds = [firstId] }, TestData.Json)).StatusCode);

        var day = DateOnly.FromDateTime(TestData.QuietPickup());
        var range = $"from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}";

        var all = await staff.GetFromJsonAsync<SearchResult>($"/api/admin/search?{range}", TestData.Json);
        Assert.Equal(2, all!.Rides.Count);
        Assert.Equal(2, all.Totals.Rides);
        Assert.Null(all.Totals.DriverPay); // dispatchers do not see pay
        Assert.All(all.Rides, r => Assert.Equal(PaymentStatus.Succeeded, r.PaymentStatus));
        Assert.Equal([ReportKind.List, ReportKind.DailySheet], all.Reports);

        var byDriver = await staff.GetFromJsonAsync<SearchResult>($"/api/admin/search?{range}&driverId={driver.Id}", TestData.Json);
        Assert.Equal(first.Reference, Assert.Single(byDriver!.Rides).Reference);
        Assert.Contains(ReportKind.Driver, byDriver.Reports);

        var unassigned = await staff.GetFromJsonAsync<SearchResult>($"/api/admin/search?{range}&unassigned=true", TestData.Json);
        Assert.Equal(second.Reference, Assert.Single(unassigned!.Rides).Reference);

        var byText = await staff.GetFromJsonAsync<SearchResult>($"/api/admin/search?{range}&text={second.Reference.ToLowerInvariant()}", TestData.Json);
        Assert.Single(byText!.Rides);
        var notOnline = await staff.GetFromJsonAsync<SearchResult>($"/api/admin/search?{range}&payment=NotPaidOnline", TestData.Json);
        Assert.Empty(notOnline!.Rides);

        foreach (var kind in new[] { "List", "DailySheet" })
        {
            var pdf = await staff.GetAsync($"/api/admin/search/report?{range}&kind={kind}");
            Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
            Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
            Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(await pdf.Content.ReadAsByteArrayAsync(), 0, 4));
        }
        var driverReport = await staff.GetAsync($"/api/admin/search/report?{range}&driverId={driver.Id}&kind=Driver");
        Assert.Equal(HttpStatusCode.OK, driverReport.StatusCode);

        var needsPartner = await staff.GetAsync($"/api/admin/search/report?{range}&kind=Partner");
        Assert.Equal(HttpStatusCode.BadRequest, needsPartner.StatusCode);
        var tooLong = await staff.GetAsync($"/api/admin/search?from=2025-01-01&to=2026-12-31");
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }
}
