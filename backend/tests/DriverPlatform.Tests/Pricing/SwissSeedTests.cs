using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.DriverPortal;
using DriverPlatform.Infrastructure.Persistence;
using DriverPlatform.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DriverPlatform.Tests.Pricing;

/// <summary>REGION=ch: a Swiss server starts with Swiss VAT, holidays and zones.</summary>
public sealed class SwissSeedTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly DbContextOptions<AppDbContext> _options =
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"seed-{Guid.NewGuid()}").Options;

    private AppDbContext Db() => new(_options, TimeProvider.System);

    private async Task SeedAsync(string region)
    {
        await using var db = Db();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Site:Region"] = region }).Build();
        await new DbSeeder(db, factory.Services.GetRequiredService<IPasswordHasher>(),
            factory.Services.GetRequiredService<IDriverLinks>(), config, NullLogger<DbSeeder>.Instance).SeedAsync();
    }

    [Fact]
    public async Task A_new_swiss_server_starts_with_swiss_vat_holidays_and_zones()
    {
        await SeedAsync("ch");

        await using var db = Db();
        Assert.Equal(8.1m, (await db.PricingSettings.SingleAsync()).VatRatePercent);
        var holidays = await db.SurchargeRules.Where(r => r.Name.StartsWith("Public holiday")).Select(r => r.Name).ToListAsync();
        Assert.Contains("Public holiday – Swiss National Day", holidays);
        Assert.DoesNotContain("Public holiday – Ferragosto", holidays);
        var zones = await db.Zones.ToListAsync();
        Assert.Contains(zones, z => z.Name == "Zurich Airport (ZRH)" && z.IsActive);
        Assert.DoesNotContain(zones, z => z.Name == "Venice historic centre");
        // Seeded names come translated.
        Assert.Equal("Flughafen Zürich (ZRH)", zones.Single(z => z.Name == "Zurich Airport (ZRH)").NameTranslations?.De);
    }

    [Fact]
    public async Task An_italian_database_switches_to_switzerland_once_and_keeps_the_italian_items_off()
    {
        await SeedAsync("it");
        await SeedAsync("ch");

        await using (var db = Db())
        {
            Assert.Equal(8.1m, (await db.PricingSettings.SingleAsync()).VatRatePercent);
            var venice = await db.Zones.SingleAsync(z => z.Name == "Venice historic centre");
            Assert.False(venice.IsActive);
            Assert.True((await db.Zones.SingleAsync(z => z.Name == "Zermatt (car-free)")).IsActive);
            var rules = await db.SurchargeRules.ToListAsync();
            Assert.False(rules.Single(r => r.Name == "Public holiday – Ferragosto").IsActive);
            // Shared holidays stay on and are not added twice.
            Assert.True(rules.Single(r => r.Name == "Public holiday – Christmas Day").IsActive);
            Assert.Single(rules, r => r.Name == "Public holiday – Easter Monday 2027");

            // The staff switch an Italian zone back on and set their own VAT.
            venice.IsActive = true;
            (await db.PricingSettings.SingleAsync()).VatRatePercent = 10m;
            await db.SaveChangesAsync();
        }

        await SeedAsync("ch");

        await using (var db = Db())
        {
            Assert.True((await db.Zones.SingleAsync(z => z.Name == "Venice historic centre")).IsActive);
            Assert.Equal(10m, (await db.PricingSettings.SingleAsync()).VatRatePercent);
            Assert.Single(await db.Zones.Where(z => z.Name == "Zurich Airport (ZRH)").ToListAsync());
        }
    }
}
