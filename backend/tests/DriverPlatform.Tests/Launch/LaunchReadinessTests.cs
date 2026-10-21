using System.Net.Http.Json;
using DriverPlatform.Application.Bookings;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Infrastructure.Persistence;
using DriverPlatform.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DriverPlatform.Tests.Launch;

public sealed class LaunchReadinessTests
{
    [Fact]
    public async Task Unpaid_bookings_are_cancelled_but_an_open_payment_page_is_respected()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var stale = await (await client.PostAsJsonAsync("/api/bookings", TestData.Booking(TestData.Trip()), TestData.Json))
            .Content.ReadFromJsonAsync<CreateBookingResponse>(TestData.Json);
        var fresh = await (await client.PostAsJsonAsync("/api/bookings", TestData.Booking(TestData.Trip()), TestData.Json))
            .Content.ReadFromJsonAsync<CreateBookingResponse>(TestData.Json);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Two days old, and its payment page expired long ago.
        var old = await db.Bookings.Include(b => b.Payments).SingleAsync(b => b.Reference == stale!.Reference);
        old.CreatedAt = DateTimeOffset.UtcNow.AddDays(-3);
        foreach (var payment in old.Payments)
        {
            payment.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-3);
        }
        // SaveChanges stamps UpdatedAt only; CreatedAt keeps the value set above.
        await db.SaveChangesAsync();
        var housekeeping = scope.ServiceProvider.GetRequiredService<IHousekeepingService>();

        var cancelled = await housekeeping.CancelUnpaidAsync(CancellationToken.None);

        Assert.Equal(1, cancelled);
        var statuses = await db.Bookings.AsNoTracking().ToDictionaryAsync(b => b.Reference, b => b.Status);
        Assert.Equal(BookingStatus.Cancelled, statuses[stale!.Reference]);
        Assert.Equal(BookingStatus.AwaitingPayment, statuses[fresh!.Reference]);
    }

    [Fact]
    public void Production_refuses_development_settings()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting("Jwt:SigningKey", "dev-only-signing-key-change-me-0123456789abcdef");
            b.UseSetting("Links:SigningKey", "another-key-that-is-long-enough-0123456789");
            b.UseSetting("Payments:Provider", "Fake");
            b.UseSetting("Payments:FrontendBaseUrl", "http://localhost:4200");
            b.UseSetting("WhatsApp:Provider", "Log");
            b.UseSetting("Database:MigrateOnStartup", "false");
            b.UseSetting("ConnectionStrings:Database", "Host=localhost;Database=none");
        });

        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Jwt:SigningKey", error.Message);
        Assert.Contains("Payments:Provider must be \"Stripe\"", error.Message);
        Assert.Contains("Email:Host", error.Message);
        Assert.Contains("Company:VatNumber", error.Message);
    }
}
