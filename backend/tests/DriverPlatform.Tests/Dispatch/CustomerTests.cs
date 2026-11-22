using System.Net;
using System.Net.Http.Json;
using DriverPlatform.Application.Customers;
using DriverPlatform.Tests.Infrastructure;

namespace DriverPlatform.Tests.Dispatch;

public sealed class CustomerTests
{
    [Fact]
    public async Task Lists_customers_with_their_rides_and_spend_and_saves_their_card()
    {
        using var factory = new ApiFactory();
        var staff = await factory.CreateDispatcherClientAsync();
        var web = factory.CreateClient();
        var first = await web.BookAndPayAsync();
        await web.BookAndPayAsync(TestData.Trip() with { PickupAt = TestData.QuietPickup().AddHours(4) });
        // A checkout never paid is not a customer yet.
        var unpaid = await web.PostAsJsonAsync("/api/bookings", TestData.Booking(TestData.Trip()) with
        {
            Contact = new() { FirstName = "Otto", LastName = "Keller", Email = "otto@example.com", Phone = "+41 79 111 22 33" },
        }, TestData.Json);
        unpaid.EnsureSuccessStatusCode();

        var list = await staff.GetFromJsonAsync<CustomerList>("/api/admin/customers", TestData.Json);
        var maria = Assert.Single(list!.Items);
        Assert.Equal(("Maria Rossi", 2, 0), (maria.Name, maria.Rides, maria.Cancelled));
        Assert.True(maria.Spent > 0);
        Assert.NotNull(maria.NextRide);
        Assert.Equal((1, 1, 1), (list.Summary.Customers, list.Summary.Returning, list.Summary.Upcoming));

        var byPhone = await staff.GetFromJsonAsync<CustomerList>("/api/admin/customers?search=333 123", TestData.Json);
        var byReference = await staff.GetFromJsonAsync<CustomerList>($"/api/admin/customers?search={first.Reference}", TestData.Json);
        var nobody = await staff.GetFromJsonAsync<CustomerList>("/api/admin/customers?search=keller", TestData.Json);
        var viaPartner = await staff.GetFromJsonAsync<CustomerList>("/api/admin/customers?filter=ViaPartner", TestData.Json);
        Assert.Single(byPhone!.Items);
        Assert.Single(byReference!.Items);
        Assert.Empty(nobody!.Items);
        Assert.Empty(viaPartner!.Items);

        var saved = await staff.PutAsJsonAsync($"/api/admin/customers/{maria.Id}", new CustomerRequest
        {
            FirstName = "Maria",
            LastName = "Rossi",
            Email = "Maria@Example.com",
            Phone = "+39 333 123 4567",
            Language = "de",
            MarketingConsent = true,
            NotesForDriver = "Prefers still water",
        }, TestData.Json);
        var badLanguage = await staff.PutAsJsonAsync($"/api/admin/customers/{maria.Id}", new CustomerRequest
        {
            FirstName = "Maria", Phone = "+39 333 123 4567", Language = "xx",
        }, TestData.Json);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badLanguage.StatusCode);
        var card = (await saved.Content.ReadFromJsonAsync<CustomerDetail>(TestData.Json))!;
        Assert.Equal(("maria@example.com", "de", "Prefers still water"), (card.Email, card.Language, card.NotesForDriver));
        Assert.Equal(2, card.History.Count);
        Assert.True(card.MarketingConsent);
    }

    [Fact]
    public async Task Needs_a_staff_login()
    {
        using var factory = new ApiFactory();
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/admin/customers")).StatusCode);
    }
}
