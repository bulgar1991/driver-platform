using System.Net;
using System.Net.Http.Json;
using DriverPlatform.Application.DriverPortal;
using DriverPlatform.Application.Quotes;
using DriverPlatform.Application.Reviews;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Infrastructure.Persistence;
using DriverPlatform.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DriverPlatform.Tests.Launch;

public sealed class HomePageApiTests
{
    private static DriverApplicationRequest Application(string phone = "+39 333 000 1111") => new()
    {
        FirstName = "Paolo",
        LastName = "Russo",
        Phone = phone,
        Email = "paolo@example.com",
        City = "Venezia",
        Licence = DriverLicenceKind.Ncc,
        HasOwnVehicle = true,
        Vehicle = "Mercedes E-Class 2023",
        YearsExperience = 8,
        Languages = ["it", "en"],
        PrivacyConsent = true,
    };

    [Fact]
    public async Task Tariffs_are_public_with_holidays_grouped()
    {
        using var factory = new ApiFactory();

        var options = await factory.CreateClient().GetFromJsonAsync<BookingOptionsResponse>("/api/booking-options", TestData.Json);

        var sedan = options!.Vehicles.Single(v => v.Category == VehicleCategory.Sedan);
        Assert.Equal(10m, sedan.BaseFee);
        Assert.Equal(1.60m, sedan.PricePerKm);
        Assert.Single(options.Surcharges, s => s.Name == "Public holidays" && s.Percent == 20m);
        Assert.Contains(options.Surcharges, s => s.Name.StartsWith("Night"));
    }

    [Fact]
    public async Task Four_tariffs_cheapest_first()
    {
        using var factory = new ApiFactory();

        var options = await factory.CreateClient().GetFromJsonAsync<BookingOptionsResponse>("/api/booking-options", TestData.Json);

        Assert.Equal(
            [VehicleCategory.Sedan, VehicleCategory.Business, VehicleCategory.Estate, VehicleCategory.Van],
            options!.Vehicles.Select(v => v.Category));
    }

    [Fact]
    public async Task A_database_with_the_first_three_tariffs_gets_business_added_and_keeps_its_own_prices()
    {
        using var factory = new ApiFactory();
        factory.CreateClient(); // starts the app, which seeds the database
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.VehicleRates.Remove(await db.VehicleRates.SingleAsync(r => r.Category == VehicleCategory.Business));
        (await db.VehicleRates.SingleAsync(r => r.Category == VehicleCategory.Sedan)).PricePerKm = 1.99m;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();

        var rates = await db.VehicleRates.AsNoTracking().ToListAsync();
        Assert.Equal(4, rates.Count);
        Assert.Equal(1.75m, rates.Single(r => r.Category == VehicleCategory.Business).PricePerKm);
        Assert.Equal(1.99m, rates.Single(r => r.Category == VehicleCategory.Sedan).PricePerKm);
    }

    [Fact]
    public async Task Driver_application_is_saved_once_and_emailed_to_the_company()
    {
        using var factory = new ApiFactory();
        factory.Emails.Sent.Clear();
        var client = factory.CreateClient();

        var first = await client.PostAsJsonAsync("/api/driver-applications", Application(), TestData.Json);
        var doubleClick = await client.PostAsJsonAsync("/api/driver-applications", Application(), TestData.Json);
        var noConsent = await client.PostAsJsonAsync("/api/driver-applications", Application("+39 333 999 0000") with { PrivacyConsent = false }, TestData.Json);
        var bot = await client.PostAsJsonAsync("/api/driver-applications", Application("+39 333 777 0000") with { Website = "http://spam" }, TestData.Json);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, doubleClick.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noConsent.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, bot.StatusCode); // the bot is not told
        var staff = await factory.CreateDispatcherClientAsync();
        var list = await staff.GetFromJsonAsync<List<DriverApplicationDto>>("/api/admin/driver-applications", TestData.Json);
        var saved = Assert.Single(list!);
        Assert.Equal("+393330001111", saved.Phone);
        Assert.Equal(DriverApplicationStatus.New, saved.Status);
        Assert.Contains(factory.Emails.Sent, e => e.Subject.StartsWith("New driver application: Paolo Russo"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/driver-applications")).StatusCode);
    }

    [Fact]
    public async Task Testimonials_show_only_consented_good_reviews_with_first_name_and_initial()
    {
        using var factory = new ApiFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var client = new Client { FirstName = "Maria", LastName = "rossi", Phone = "+39333", Email = "m@example.com" };
            var driver = new Driver { FirstName = "Marco", LastName = "Bianchi", Phone = "+39340" };
            Review Review(int rating, bool consent, string? comment) => new()
            {
                Booking = new Domain.Entities.Booking { Reference = $"MD-T{rating}{(consent ? 1 : 0)}XYZ", Client = client, VehicleCategory = VehicleCategory.Van },
                Driver = driver,
                TokenHash = Guid.NewGuid().ToString("N"),
                Rating = rating,
                Comment = comment,
                PublishConsent = consent,
                SentAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
                SubmittedAt = DateTimeOffset.UtcNow,
            };
            db.Reviews.AddRange(Review(5, true, "Wonderful driver"), Review(5, false, "Private"), Review(2, true, "Late"));
            await db.SaveChangesAsync();
        }

        var testimonials = await factory.CreateClient().GetFromJsonAsync<List<Testimonial>>("/api/testimonials", TestData.Json);

        var only = Assert.Single(testimonials!);
        Assert.Equal("Maria R.", only.Author);
        Assert.Equal("Wonderful driver", only.Comment);
    }
}
