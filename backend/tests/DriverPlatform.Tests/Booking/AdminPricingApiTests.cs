using System.Net;
using System.Net.Http.Json;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Admin;
using DriverPlatform.Application.Auth;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;
using DriverPlatform.Infrastructure.Persistence;
using DriverPlatform.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;

namespace DriverPlatform.Tests.Booking;

public sealed class AdminPricingApiTests
{
    [Fact]
    public async Task Pricing_admin_requires_the_admin_role()
    {
        using var factory = new ApiFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.StaffUsers.Add(new StaffUser
            {
                Email = "dispatcher@test.local",
                FullName = "Dispatcher",
                Role = StaffRole.Dispatcher,
                PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash("dispatcher-pass-123"),
            });
            await db.SaveChangesAsync();
        }
        var dispatcher = factory.CreateClient();
        var login = await dispatcher.PostAsJsonAsync("/api/auth/login", new LoginRequest("dispatcher@test.local", "dispatcher-pass-123"));
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>(TestData.Json))!.AccessToken;
        dispatcher.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/admin/pricing")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await dispatcher.GetAsync("/api/admin/pricing")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await dispatcher.GetAsync("/api/admin/zones")).StatusCode);
    }

    [Fact]
    public async Task New_tariff_applies_to_the_next_quote_and_is_audited()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();

        var update = await admin.PutAsJsonAsync("/api/admin/pricing/rates/Sedan",
            new UpdateVehicleRateRequest { BaseFee = 20, PricePerKm = 2, MaxPassengers = 3, MaxLuggage = 3 }, TestData.Json);
        update.EnsureSuccessStatusCode();
        var quote = await factory.CreateClient().QuoteAsync(TestData.Trip());

        var sedan = quote.Vehicles.Single(v => v.Category == VehicleCategory.Sedan);
        Assert.Equal(Math.Round(20m + quote.Outbound.DistanceKm * 2m, 2), sedan.Total);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "pricing.rate_updated" && a.Actor == ApiFactory.AdminEmail));
    }

    [Fact]
    public async Task Surcharge_rules_can_be_added_and_apply_immediately()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();
        var pickup = TestData.QuietPickup();

        var created = await admin.PostAsJsonAsync("/api/admin/pricing/surcharges", new SurchargeRuleRequest
        {
            Name = "Biennale opening",
            Kind = SurchargeKind.DateRange,
            Percent = 25,
            StartDate = DateOnly.FromDateTime(pickup),
            EndDate = DateOnly.FromDateTime(pickup),
        }, TestData.Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var quote = await factory.CreateClient().QuoteAsync(TestData.Trip());
        Assert.Equal("Biennale opening", Assert.Single(quote.Outbound.Surcharges).Name);

        var invalid = await admin.PostAsJsonAsync("/api/admin/pricing/surcharges", new SurchargeRuleRequest
        {
            Name = "No days",
            Kind = SurchargeKind.DaysOfWeek,
            Percent = 10,
        }, TestData.Json);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task No_car_zone_needs_a_meeting_point()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();
        var zone = new ZoneRequest
        {
            Name = "Burano",
            Type = ZoneType.NoCarAccess,
            Shape = ZoneShape.Circle,
            CenterLatitude = 45.4853,
            CenterLongitude = 12.4167,
            RadiusMeters = 600,
        };

        var refused = await admin.PostAsJsonAsync("/api/admin/zones", zone, TestData.Json);
        var created = await admin.PostAsJsonAsync("/api/admin/zones", zone with
        {
            MeetingPoints = [new MeetingPoint { Name = "Treporti", Address = "Punta Sabbioni", Latitude = 45.4430, Longitude = 12.4230 }],
        }, TestData.Json);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }
}
