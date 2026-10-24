using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DriverPlatform.Application.Dispatch;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Infrastructure.Persistence;
using DriverPlatform.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DriverPlatform.Tests.Dispatch;

/// <summary>Pictures and deletion of vehicles and drivers.</summary>
public sealed class FleetPhotoTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 5, 6, 7, 8];

    private static VehicleRequest Vehicle(string plate) => new()
    {
        PlateNumber = plate,
        Make = "Mercedes-Benz",
        Model = "E-Class",
        Category = VehicleCategory.Business,
        Seats = 3,
        LuggageCapacity = 2,
    };

    private static DriverRequest Driver(string phone) => new()
    {
        FirstName = "Giulia",
        LastName = "Rossi",
        Phone = phone,
        Documents = [new DriverDocumentDto { Type = DriverDocumentType.DrivingLicence }],
    };

    private static MultipartFormDataContent File(byte[] data, string contentType = "image/png")
    {
        var file = new ByteArrayContent(data);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", "photo" } };
    }

    private static async Task<T> Read<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(TestData.Json))!;
    }

    [Fact]
    public async Task Vehicle_photo_is_uploaded_replaced_served_and_removed()
    {
        using var factory = new ApiFactory();
        var staff = await factory.CreateDispatcherClientAsync();
        var vehicle = await Read<VehicleDto>(await staff.PostAsJsonAsync("/api/admin/vehicles", Vehicle("GA123BC"), TestData.Json), HttpStatusCode.Created);
        Assert.Null(vehicle.PhotoId);

        var first = await Read<VehicleDto>(await staff.PutAsync($"/api/admin/vehicles/{vehicle.Id}/photo", File(Png)));
        var second = await Read<VehicleDto>(await staff.PutAsync($"/api/admin/vehicles/{vehicle.Id}/photo", File(Jpeg)));
        Assert.NotEqual(first.PhotoId, second.PhotoId);

        // Served without a token, as the browser's <img> asks for it; the type comes from the bytes.
        using var anonymous = factory.CreateClient();
        var served = await anonymous.GetAsync($"/api/photos/{second.PhotoId}");
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("image/jpeg", served.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Jpeg, await served.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/photos/{first.PhotoId}")).StatusCode);

        var removed = await Read<VehicleDto>(await staff.DeleteAsync($"/api/admin/vehicles/{vehicle.Id}/photo"));
        Assert.Null(removed.PhotoId);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/photos/{second.PhotoId}")).StatusCode);
    }

    [Fact]
    public async Task A_file_that_is_not_a_picture_is_refused()
    {
        using var factory = new ApiFactory();
        var staff = await factory.CreateDispatcherClientAsync();
        var driver = await Read<DriverDto>(await staff.PostAsJsonAsync("/api/admin/drivers", Driver("+393400000011"), TestData.Json), HttpStatusCode.Created);

        // Claims to be a PNG; the bytes say otherwise.
        var response = await staff.PutAsync($"/api/admin/drivers/{driver.Id}/photo", File("<svg onload=alert(1)>"u8.ToArray()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var withPhoto = await Read<DriverDto>(await staff.PutAsync($"/api/admin/drivers/{driver.Id}/photo", File(Png)));
        Assert.NotNull(withPhoto.PhotoId);
    }

    [Fact]
    public async Task Photo_upload_needs_staff()
    {
        using var factory = new ApiFactory();
        var staff = await factory.CreateDispatcherClientAsync();
        var vehicle = await Read<VehicleDto>(await staff.PostAsJsonAsync("/api/admin/vehicles", Vehicle("GA123BC"), TestData.Json), HttpStatusCode.Created);

        using var anonymous = factory.CreateClient();
        var response = await anonymous.PutAsync($"/api/admin/vehicles/{vehicle.Id}/photo", File(Png));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unused_vehicle_and_driver_are_deleted_with_their_photos_and_documents()
    {
        using var factory = new ApiFactory();
        var staff = await factory.CreateDispatcherClientAsync();
        var vehicle = await Read<VehicleDto>(await staff.PostAsJsonAsync("/api/admin/vehicles", Vehicle("GA123BC"), TestData.Json), HttpStatusCode.Created);
        var driver = await Read<DriverDto>(await staff.PostAsJsonAsync("/api/admin/drivers", Driver("+393400000011"), TestData.Json), HttpStatusCode.Created);
        await staff.PutAsync($"/api/admin/vehicles/{vehicle.Id}/photo", File(Png));
        await staff.PutAsync($"/api/admin/drivers/{driver.Id}/photo", File(Jpeg));

        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync($"/api/admin/vehicles/{vehicle.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync($"/api/admin/drivers/{driver.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.DeleteAsync($"/api/admin/drivers/{driver.Id}")).StatusCode);

        var vehicles = await Read<List<VehicleDto>>(await staff.GetAsync("/api/admin/vehicles?includeInactive=true"));
        var drivers = await Read<List<DriverDto>>(await staff.GetAsync("/api/admin/drivers?includeInactive=true"));
        Assert.Empty(vehicles);
        Assert.Empty(drivers);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Photos.AnyAsync());
        Assert.False(await db.DriverDocuments.AnyAsync());
        Assert.Contains(await db.AuditLogs.Select(a => a.Action).ToListAsync(), a => a == "vehicle.deleted");
    }

    [Fact]
    public async Task Vehicle_and_driver_used_on_a_shift_are_kept()
    {
        using var factory = new ApiFactory();
        var staff = await factory.CreateDispatcherClientAsync();
        var vehicle = await Read<VehicleDto>(await staff.PostAsJsonAsync("/api/admin/vehicles", Vehicle("GA123BC"), TestData.Json), HttpStatusCode.Created);
        var driver = await Read<DriverDto>(await staff.PostAsJsonAsync("/api/admin/drivers", Driver("+393400000011"), TestData.Json), HttpStatusCode.Created);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Assignments.Add(new Assignment { DriverId = driver.Id, VehicleId = vehicle.Id, Status = AssignmentStatus.Completed });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Conflict, (await staff.DeleteAsync($"/api/admin/vehicles/{vehicle.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await staff.DeleteAsync($"/api/admin/drivers/{driver.Id}")).StatusCode);
        Assert.Single(await Read<List<VehicleDto>>(await staff.GetAsync("/api/admin/vehicles")));
        Assert.Single(await Read<List<DriverDto>>(await staff.GetAsync("/api/admin/drivers")));
    }
}
