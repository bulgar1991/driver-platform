using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DriverPlatform.Application.Auth;
using DriverPlatform.Application.Bookings;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Quotes;
using DriverPlatform.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace DriverPlatform.Tests.Infrastructure;

public static class TestData
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static readonly PointInput RomeTermini = new() { Address = "Roma Termini", Latitude = 41.9010, Longitude = 12.5016 };
    public static readonly PointInput RomeEur = new() { Address = "EUR, Roma", Latitude = 41.8300, Longitude = 12.4700 };
    public static readonly PointInput VeniceRialto = new() { Address = "Ponte di Rialto, Venezia", Latitude = 45.4380, Longitude = 12.3359 };
    public static readonly PointInput VeniceAirport = new() { Address = "Aeroporto Marco Polo", Latitude = 45.5050, Longitude = 12.3420 };

    /// <summary>A weekday afternoon outside every seeded surcharge: not Fri–Sun, July–August or a holiday.</summary>
    public static DateTime QuietPickup(int daysAhead = 10)
    {
        var date = ItalyTime.ToLocal(DateTimeOffset.UtcNow).Date.AddDays(daysAhead);
        while (date.DayOfWeek is not (DayOfWeek.Tuesday or DayOfWeek.Wednesday)
            || date.Month is 7 or 8
            || (date.Month, date.Day) is (1, 1) or (1, 6) or (4, 25) or (5, 1) or (6, 2) or (11, 1) or (12, 8) or (12, 25) or (12, 26))
        {
            date = date.AddDays(1);
        }
        return date.AddHours(14);
    }

    public static QuoteRequest Trip(PointInput? pickup = null, PointInput? dropoff = null) => new()
    {
        Pickup = pickup ?? RomeTermini,
        Dropoff = dropoff ?? RomeEur,
        PickupAt = QuietPickup(),
        Passengers = 2,
        LargeLuggage = 2,
    };

    public static CreateBookingRequest Booking(QuoteRequest trip, decimal? expectedTotal = null) => new()
    {
        Trip = trip,
        VehicleCategory = VehicleCategory.Sedan,
        ExpectedTotal = expectedTotal,
        Contact = new ContactInput
        {
            FirstName = "Maria",
            LastName = "Rossi",
            Email = "maria@example.com",
            Phone = "+39 333 123 4567",
        },
        GdprConsent = true,
    };

    public static async Task<QuoteResponse> QuoteAsync(this HttpClient client, QuoteRequest trip)
    {
        var response = await client.PostAsJsonAsync("/api/quotes", trip, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<QuoteResponse>(Json))!;
    }

    /// <summary>Books through the API and pays on the fake payment page: the booking ends up "New".</summary>
    public static async Task<CreateBookingResponse> BookAndPayAsync(
        this HttpClient client, QuoteRequest? trip = null, VehicleCategory category = VehicleCategory.Sedan)
    {
        var response = await client.PostAsJsonAsync("/api/bookings", Booking(trip ?? Trip()) with { VehicleCategory = category }, Json);
        response.EnsureSuccessStatusCode();
        var booking = (await response.Content.ReadFromJsonAsync<CreateBookingResponse>(Json))!;
        var session = booking.PaymentUrl!.Split('/').Last();
        (await client.PostAsJsonAsync($"/api/dev/payments/{session}/complete", new { method = "Card" }, Json)).EnsureSuccessStatusCode();
        return booking;
    }

    public static async Task<HttpClient> CreateDispatcherClientAsync(this ApiFactory factory)
    {
        const string email = "dispatcher@test.local";
        const string password = "dispatcher-pass-123";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DriverPlatform.Infrastructure.Persistence.AppDbContext>();
            if (!db.StaffUsers.Any(u => u.Email == email))
            {
                db.StaffUsers.Add(new DriverPlatform.Domain.Entities.StaffUser
                {
                    Email = email,
                    FullName = "Dispatcher",
                    Role = StaffRole.Dispatcher,
                    PasswordHash = scope.ServiceProvider.GetRequiredService<DriverPlatform.Application.Abstractions.IPasswordHasher>().Hash(password),
                });
                await db.SaveChangesAsync();
            }
        }
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    public static async Task<HttpClient> CreateAdminClientAsync(this ApiFactory factory)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(ApiFactory.AdminEmail, ApiFactory.AdminPassword));
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }
}
