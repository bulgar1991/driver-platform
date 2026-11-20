using System.Net;
using System.Net.Http.Json;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Visitors;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Infrastructure.Persistence;
using DriverPlatform.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DriverPlatform.Tests.Visitors;

/// <summary>Admin → Visitors: the site's own statistics.</summary>
public sealed class VisitorTests
{
    private const string Iphone = "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1";
    private const string WindowsChrome = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36";

    private sealed class FakeGeo : IGeoIpLookup
    {
        public (string? Country, string? City) Lookup(IPAddress address) =>
            address.ToString() == "203.0.113.7" ? ("CH", "Zurich") : ("DE", "Munich");
    }

    private static async Task Visit(HttpClient client, string path, string ip, string userAgent, string? referrer = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/visits")
        {
            Content = JsonContent.Create(new VisitRequest(path, referrer, "en")),
        };
        request.Headers.Add("X-Forwarded-For", ip);
        request.Headers.UserAgent.ParseAdd(userAgent);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Public_pages_are_counted_with_place_device_and_ip_private_ones_and_bots_are_not()
    {
        using var factory = new ApiFactory();
        var web = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IGeoIpLookup>();
            s.AddSingleton<IGeoIpLookup, FakeGeo>();
        }));
        var client = web.CreateClient();

        await Visit(client, "/tariffs?utm_source=x", "203.0.113.7", Iphone, "https://www.google.com/search?q=transfer");
        await Visit(client, "/", "203.0.113.7", Iphone);
        await Visit(client, "/de/", "198.51.100.4", WindowsChrome);
        await Visit(client, "/my-booking/MD-ABC/secret-token", "198.51.100.4", WindowsChrome);
        await Visit(client, "/de/review/secret", "198.51.100.4", WindowsChrome);
        await Visit(client, "/", "198.51.100.9", "Googlebot/2.1 (+http://www.google.com/bot.html)");

        using var scope = web.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var visits = await db.PageVisits.OrderBy(v => v.At).ToListAsync();
        Assert.Equal(3, visits.Count);
        var first = visits[0];
        Assert.Equal("/tariffs", first.Path);
        Assert.Equal("www.google.com", first.Referrer);
        Assert.Equal("203.0.113.7", first.Ip);
        Assert.Equal(("CH", "Zurich"), (first.Country, first.City));
        Assert.Equal(("mobile", "Safari", "iOS"), (first.Device, first.Browser, first.Os));
        Assert.Equal(("desktop", "Chrome", "Windows"), (visits[2].Device, visits[2].Browser, visits[2].Os));

        // The summary: 3 visits by 2 visitors, Switzerland first.
        var admin = await web.CreateAdminClientAsync();
        var summary = (await admin.GetFromJsonAsync<VisitorSummary>("/api/admin/visitors?days=7", TestData.Json))!;
        Assert.Equal(3, summary.Visits);
        Assert.Equal(2, summary.Visitors);
        Assert.Equal(3, summary.VisitsToday);
        Assert.Equal(7, summary.ByDay.Count);
        Assert.Equal(new CountItem("CH", 2), summary.Countries[0]);
        Assert.Contains(summary.Cities, c => c.City == "Munich");
        Assert.Contains(summary.Referrers, r => r.Key == "www.google.com");
        Assert.Equal("198.51.100.4", summary.Recent[0].Ip);
    }

    [Fact]
    public async Task Only_admins_see_visitors()
    {
        using var factory = new ApiFactory();
        var dispatcher = await factory.CreateDispatcherClientAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await dispatcher.GetAsync("/api/admin/visitors")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/admin/visitors")).StatusCode);
    }

    [Fact]
    public async Task Ips_are_cleared_after_30_days_and_visits_deleted_after_13_months()
    {
        using var factory = new ApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;
        db.PageVisits.AddRange(
            new PageVisit { At = now.AddDays(-1), Path = "/", Ip = "203.0.113.1", VisitorHash = "a" },
            new PageVisit { At = now.AddDays(-31), Path = "/", Ip = "203.0.113.2", VisitorHash = "b" },
            new PageVisit { At = now.AddDays(-401), Path = "/", Ip = null, VisitorHash = "c" });
        await db.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<IVisitorService>().CleanUpAsync(default);

        db.ChangeTracker.Clear();
        var left = await db.PageVisits.OrderByDescending(v => v.At).ToListAsync();
        Assert.Equal(2, left.Count);
        Assert.Equal("203.0.113.1", left[0].Ip);
        Assert.Null(left[1].Ip);
    }
}
