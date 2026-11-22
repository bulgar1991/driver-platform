using System.Net;
using System.Net.Http.Json;
using DriverPlatform.Application.Dispatch;
using DriverPlatform.Application.Geo;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Tests.Infrastructure;

namespace DriverPlatform.Tests.Dispatch;

public sealed class ExpirationTests
{
    private static DateOnly Today() => DateOnly.FromDateTime(OfficeTime.ToLocal(DateTimeOffset.UtcNow));

    private static async Task<T> Read<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(TestData.Json))!;
    }

    [Fact]
    public async Task Lists_every_deadline_most_urgent_first_and_renews_them()
    {
        using var factory = new ApiFactory();
        var staff = await factory.CreateDispatcherClientAsync();
        var driver = await Read<DriverDto>(await staff.PostAsJsonAsync("/api/admin/drivers", new DriverRequest
        {
            FirstName = "Anna",
            LastName = "Bianchi",
            Phone = "+393400000071",
            Documents =
            [
                new DriverDocumentDto { Type = DriverDocumentType.DrivingLicence, Number = "U1234", ExpiresOn = Today().AddDays(-2) },
                new DriverDocumentDto { Type = DriverDocumentType.NccLicence, ExpiresOn = Today().AddYears(2) },
            ],
        }, TestData.Json), HttpStatusCode.Created);
        var vehicle = await Read<VehicleDto>(await staff.PostAsJsonAsync("/api/admin/vehicles", new VehicleRequest
        {
            PlateNumber = "ZH 7001",
            Make = "Mercedes-Benz",
            Model = "V",
            Category = VehicleCategory.Van,
            Seats = 7,
            LuggageCapacity = 7,
            InsuranceExpiresOn = Today().AddDays(10),
            MileageKm = 50_000,
        }, TestData.Json), HttpStatusCode.Created);
        var service = await Read<VehicleExpirationDto>(await staff.PostAsJsonAsync($"/api/admin/vehicles/{vehicle.Id}/expirations",
            new VehicleExpirationRequest { Title = "Tyres", DueKm = 50_400 }, TestData.Json), HttpStatusCode.Created);

        var list = await Read<ExpirationsResponse>(await staff.GetAsync("/api/admin/expirations"));

        var licence = list.Items.Single(i => i.DocumentType == DriverDocumentType.DrivingLicence);
        var insurance = list.Items.Single(i => i.Source == DeadlineSource.Insurance);
        var tyres = list.Items.Single(i => i.RecordId == service.Id);
        Assert.Equal(licence, list.Items[0]);
        Assert.Equal((DeadlineState.Expired, -2, "U1234"), (licence.State, licence.DaysLeft, licence.Reference));
        Assert.Equal((DeadlineState.DueSoon, 10), (insurance.State, insurance.DaysLeft));
        Assert.Equal((DeadlineState.DueSoon, 400), (tyres.State, tyres.KmLeft));
        Assert.Equal(DeadlineState.Later, list.Items.Single(i => i.DocumentType == DriverDocumentType.NccLicence).State);
        // No inspection date: the car cannot be dispatched, and the page says so.
        Assert.Contains(list.Missing, m => m.OwnerId == vehicle.Id);

        var renewed = await staff.PutAsJsonAsync("/api/admin/expirations/renew", new RenewDeadlineRequest
        {
            Source = DeadlineSource.DriverDocument, OwnerId = driver.Id, RecordId = licence.RecordId, DueOn = Today().AddYears(10),
        }, TestData.Json);
        var noDate = await staff.PutAsJsonAsync("/api/admin/expirations/renew", new RenewDeadlineRequest
        {
            Source = DeadlineSource.Insurance, OwnerId = vehicle.Id,
        }, TestData.Json);
        await staff.PutAsJsonAsync("/api/admin/expirations/renew", new RenewDeadlineRequest
        {
            Source = DeadlineSource.VehicleDeadline, OwnerId = vehicle.Id, RecordId = service.Id, DueKm = 90_000,
        }, TestData.Json);

        var after = await Read<ExpirationsResponse>(await staff.GetAsync("/api/admin/expirations"));
        Assert.Equal(HttpStatusCode.NoContent, renewed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noDate.StatusCode);
        Assert.Equal(DeadlineState.Later, after.Items.Single(i => i.RecordId == licence.RecordId).State);
        Assert.Equal(40_000, after.Items.Single(i => i.RecordId == service.Id).KmLeft);
    }

    [Fact]
    public async Task Needs_a_staff_login()
    {
        using var factory = new ApiFactory();
        var response = await factory.CreateClient().GetAsync("/api/admin/expirations");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
