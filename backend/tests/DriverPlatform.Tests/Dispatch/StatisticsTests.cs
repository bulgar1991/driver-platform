using System.Net;
using System.Net.Http.Json;
using DriverPlatform.Application.Statistics;
using DriverPlatform.Tests.Infrastructure;

namespace DriverPlatform.Tests.Dispatch;

public sealed class StatisticsTests
{
    [Fact]
    public async Task Counts_rides_and_revenue_of_the_period_and_hides_pay_from_dispatchers()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();
        var dispatcher = await factory.CreateDispatcherClientAsync();
        var web = factory.CreateClient();
        await web.BookAndPayAsync();
        await web.BookAndPayAsync(TestData.Trip() with { PickupAt = TestData.QuietPickup().AddHours(3) });

        var day = DateOnly.FromDateTime(TestData.QuietPickup());
        var url = $"/api/admin/statistics?from={day.AddDays(-6):yyyy-MM-dd}&to={day:yyyy-MM-dd}";
        var stats = (await admin.GetFromJsonAsync<StatisticsResult>(url, TestData.Json))!;
        var seen = (await dispatcher.GetFromJsonAsync<StatisticsResult>(url, TestData.Json))!;

        Assert.Equal((2, 1, 1), (stats.Totals.Rides, stats.Totals.Customers, stats.Totals.NewCustomers));
        Assert.True(stats.Totals.Revenue > 0);
        Assert.Equal(0, stats.Previous.Rides);
        Assert.Equal(day.AddDays(-7), stats.PreviousTo);
        Assert.Equal((StatisticsBucket.Day, 7), (stats.Bucket, stats.Series.Count));
        Assert.Equal(2, stats.Series[^1].Rides);
        Assert.Equal("Website", Assert.Single(stats.Channels).Key);
        Assert.Equal(2, stats.Heatmap.Sum(d => d.Sum()));
        Assert.Equal(2, stats.LeadTime.Sum());
        Assert.NotNull(stats.Totals.DriverPay);
        Assert.Null(seen.Totals.DriverPay);

        var tooLong = await admin.GetAsync("/api/admin/statistics?from=2025-01-01&to=2026-06-01");
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await web.GetAsync(url)).StatusCode);
    }

    [Theory]
    [InlineData("2026-11-26", StatisticsBucket.Week, "2026-11-23")]
    [InlineData("2026-11-23", StatisticsBucket.Week, "2026-11-23")]
    [InlineData("2026-11-29", StatisticsBucket.Week, "2026-11-23")]
    [InlineData("2026-11-26", StatisticsBucket.Month, "2026-11-01")]
    public void Buckets_start_on_monday_or_the_first(string day, StatisticsBucket bucket, string start) =>
        Assert.Equal(DateOnly.Parse(start), StatisticsService.BucketStart(DateOnly.Parse(day), bucket));

    [Theory]
    [InlineData("2026-11-01", "2026-11-30", "2026-10-01", "2026-10-31")]
    [InlineData("2026-01-01", "2026-03-31", "2025-10-01", "2025-12-31")]
    [InlineData("2026-03-01", "2026-03-31", "2026-02-01", "2026-02-28")]
    [InlineData("2026-11-10", "2026-11-16", "2026-11-03", "2026-11-09")]
    public void Compares_with_the_period_before(string from, string to, string prevFrom, string prevTo) =>
        Assert.Equal((DateOnly.Parse(prevFrom), DateOnly.Parse(prevTo)),
            StatisticsService.PreviousPeriod(DateOnly.Parse(from), DateOnly.Parse(to)));
}
