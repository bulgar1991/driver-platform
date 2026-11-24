using System.Net;
using System.Net.Http.Json;
using DriverPlatform.Application.Admin;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Tests.Infrastructure;

namespace DriverPlatform.Tests.Booking;

/// <summary>
/// Tariff names are kept in one list per process (VehicleClasses: one server per process in
/// production). Every test server resets that list when it starts, so these tests, which check
/// names, run alone: never at the same time as any other test.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TariffNamesCollection
{
    public const string Name = "Tariff names";
}

/// <summary>Admin → Pricing: tariffs are added, deleted (or hidden when used) and shown again.</summary>
[Collection(TariffNamesCollection.Name)]
public sealed class CustomTariffTests
{
    private static CreateVehicleRateRequest Minibus => new()
    {
        BaseFee = 30,
        PricePerKm = 3,
        MaxPassengers = 16,
        MaxLuggage = 16,
        Names = new VehicleClassNames("Minibus 16", "Minibus 16 posti", null, null),
    };

    private static async Task<T> Read<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(TestData.Json))!;
    }

    [Fact]
    public async Task A_new_tariff_is_priced_offered_in_its_language_and_booked()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();

        var created = await Read<VehicleRateDto>(await admin.PostAsJsonAsync("/api/admin/pricing/rates", Minibus, TestData.Json), HttpStatusCode.Created);
        Assert.Equal("Minibus16", created.Category.Code);
        Assert.False(created.IsBuiltIn);

        // Twelve people: only the minibus takes them.
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("it");
        var quote = await client.QuoteAsync(TestData.Trip() with { Passengers = 12 });
        var minibus = quote.Vehicles.Single(v => v.Category == created.Category);
        Assert.True(minibus.Available);
        Assert.Equal("Minibus 16 posti", minibus.Name);
        Assert.Equal(Math.Round(30m + quote.Outbound.DistanceKm * 3m, 2), minibus.Total);
        Assert.Contains(minibus.Outbound.Lines, l => l.Label.StartsWith("Minibus 16 posti"));
        Assert.All(quote.Vehicles.Where(v => v.Category != created.Category), v => Assert.False(v.Available));

        var booking = await client.BookAndPayAsync(TestData.Trip() with { Passengers = 12 }, created.Category);
        Assert.Equal(created.Category, booking.VehicleCategory);

        // The staff's car form lists it.
        var dispatcher = await factory.CreateDispatcherClientAsync();
        var classes = await dispatcher.GetFromJsonAsync<List<VehicleClassDto>>("/api/admin/vehicle-classes", TestData.Json);
        Assert.Contains(classes!, c => c.Category == created.Category && c.Names.En == "Minibus 16");
    }

    [Fact]
    public async Task An_unused_tariff_is_deleted_a_used_one_is_hidden_and_can_come_back()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();
        var created = await Read<VehicleRateDto>(await admin.PostAsJsonAsync("/api/admin/pricing/rates", Minibus, TestData.Json), HttpStatusCode.Created);

        var deleted = await Read<DeleteVehicleRateResult>(await admin.DeleteAsync($"/api/admin/pricing/rates/{created.Category}"));
        Assert.True(deleted.Deleted);
        Assert.DoesNotContain(deleted.Rates, r => r.Category == created.Category);

        // Standard has bookings: hidden, not deleted.
        await factory.CreateClient().BookAndPayAsync();
        var hidden = await Read<DeleteVehicleRateResult>(await admin.DeleteAsync("/api/admin/pricing/rates/Sedan"));
        Assert.True(hidden.Hidden);
        Assert.False(hidden.Rates.Single(r => r.Category == VehicleCategory.Sedan).IsActive);
        var quote = await factory.CreateClient().QuoteAsync(TestData.Trip());
        Assert.DoesNotContain(quote.Vehicles, v => v.Category == VehicleCategory.Sedan);

        var shown = await Read<VehicleRateDto>(await admin.PutAsJsonAsync("/api/admin/pricing/rates/Sedan/active", new VehicleRateActiveRequest(true), TestData.Json));
        Assert.True(shown.IsActive);
        quote = await factory.CreateClient().QuoteAsync(TestData.Trip());
        Assert.Contains(quote.Vehicles, v => v.Category == VehicleCategory.Sedan);
    }

    [Fact]
    public async Task The_last_visible_tariff_stays_and_a_name_is_required()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();
        foreach (var code in new[] { "Business", "Estate", "Van" })
        {
            (await admin.DeleteAsync($"/api/admin/pricing/rates/{code}")).EnsureSuccessStatusCode();
        }

        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync("/api/admin/pricing/rates/Sedan")).StatusCode);

        var nameless = Minibus with { Names = new VehicleClassNames(" ", null, null, null) };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/pricing/rates", nameless, TestData.Json)).StatusCode);
    }

    [Fact]
    public async Task Dispatchers_cannot_change_tariffs()
    {
        using var factory = new ApiFactory();
        var dispatcher = await factory.CreateDispatcherClientAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await dispatcher.PostAsJsonAsync("/api/admin/pricing/rates", Minibus, TestData.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await dispatcher.DeleteAsync("/api/admin/pricing/rates/Sedan")).StatusCode);
    }

    [Fact]
    public async Task The_partner_api_documents_the_class_as_a_text_code()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateClient().GetAsync("/api/partner/openapi/partner.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = System.Text.Json.Nodes.JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var schemas = doc["components"]?["schemas"]?.AsObject();
        Assert.NotNull(schemas);
        var category = schemas!.FirstOrDefault(s => s.Key.Contains("VehicleCategory")).Value;
        // No "code" object: the same string as before ("Sedan", "Minibus16").
        Assert.True(category is null || category["type"]?.ToString() == "string", category?.ToJsonString());
    }
}
