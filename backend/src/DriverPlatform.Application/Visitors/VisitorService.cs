using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Visitors;

public sealed record VisitRequest(string Path, string? Referrer, string? Language);

public sealed record CountItem(string Key, int Count);

public sealed record CityItem(string? Country, string City, int Count);

public sealed record DayItem(DateOnly Date, int Visits, int Visitors);

public sealed record RecentVisit(
    DateTimeOffset At, string Path, string? Referrer, string? Ip, string? Country, string? City,
    string Device, string? Browser, string? Os, string? Language);

/// <summary>Admin → Visitors for the last <c>Days</c> days.</summary>
public sealed record VisitorSummary(
    int Days,
    int Visits,
    int Visitors,
    int VisitsToday,
    IReadOnlyList<DayItem> ByDay,
    IReadOnlyList<CountItem> Countries,
    IReadOnlyList<CityItem> Cities,
    IReadOnlyList<CountItem> Pages,
    IReadOnlyList<CountItem> Referrers,
    IReadOnlyList<CountItem> Devices,
    IReadOnlyList<CountItem> Browsers,
    IReadOnlyList<RecentVisit> Recent);

public interface IVisitorService
{
    Task RecordAsync(VisitRequest request, IPAddress? ip, string? userAgent, string? ownHost, CancellationToken cancellationToken);
    Task<VisitorSummary> SummaryAsync(int days, CancellationToken cancellationToken);
    /// <summary>Clears IPs after 30 days and deletes visits after 13 months; returns rows touched.</summary>
    Task<int> CleanUpAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The site's own visitor statistics: every page seen on the public website, with the visitor's IP,
/// country and city (offline lookup: nothing is sent to another company), device and browser.
/// </summary>
public sealed partial class VisitorService(IAppDbContext db, IGeoIpLookup geo, TimeProvider clock, byte[] hashKey) : IVisitorService
{
    public static readonly TimeSpan KeepIp = TimeSpan.FromDays(30);
    public static readonly TimeSpan KeepVisits = TimeSpan.FromDays(400);

    /// <summary>Pages with a private link (booking, review, driver) and the panel are never recorded.</summary>
    private static readonly Regex PrivatePath = new(@"^/(?:[a-z]{2}/)?(my-booking|review|driver|admin|login|dev)(/|$)", RegexOptions.Compiled);

    public async Task RecordAsync(VisitRequest request, IPAddress? ip, string? userAgent, string? ownHost, CancellationToken cancellationToken)
    {
        var path = CleanPath(request.Path);
        if (path is null || PrivatePath.IsMatch(path) || IsBot(userAgent))
        {
            return;
        }
        var (country, city) = ip is null || IsLocal(ip) ? (null, null) : geo.Lookup(ip);
        db.PageVisits.Add(new PageVisit
        {
            At = clock.GetUtcNow(),
            Path = path,
            Referrer = ReferrerHost(request.Referrer, ownHost),
            Ip = ip?.ToString(),
            Country = country,
            City = city?.Length > 100 ? city[..100] : city,
            Device = Device(userAgent),
            Browser = Browser(userAgent),
            Os = Os(userAgent),
            Language = request.Language is { Length: 2 } language ? language.ToLowerInvariant() : null,
            VisitorHash = Hash($"{ip}|{userAgent}"),
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<VisitorSummary> SummaryAsync(int days, CancellationToken cancellationToken)
    {
        days = Math.Clamp(days, 1, 400);
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(Geo.OfficeTime.ToLocal(now));
        var from = Geo.OfficeTime.ToUtc(today.AddDays(1 - days).ToDateTime(TimeOnly.MinValue))!.Value;
        var visits = await db.PageVisits.AsNoTracking().Where(v => v.At >= from).ToListAsync(cancellationToken);

        static IReadOnlyList<CountItem> Top(IEnumerable<string?> keys, int take = 15) => keys
            .Where(k => !string.IsNullOrEmpty(k))
            .GroupBy(k => k!).Select(g => new CountItem(g.Key, g.Count()))
            .OrderByDescending(c => c.Count).ThenBy(c => c.Key).Take(take).ToList();

        var byDay = Enumerable.Range(0, days).Select(i => today.AddDays(1 - days + i)).Select(date =>
        {
            var day = visits.Where(v => DateOnly.FromDateTime(Geo.OfficeTime.ToLocal(v.At)) == date).ToList();
            return new DayItem(date, day.Count, day.Select(v => v.VisitorHash).Distinct().Count());
        }).ToList();

        return new VisitorSummary(
            days,
            visits.Count,
            visits.Select(v => v.VisitorHash).Distinct().Count(),
            byDay[^1].Visits,
            byDay,
            Top(visits.Select(v => v.Country)),
            visits.Where(v => !string.IsNullOrEmpty(v.City))
                .GroupBy(v => (v.Country, v.City!)).Select(g => new CityItem(g.Key.Country, g.Key.Item2, g.Count()))
                .OrderByDescending(c => c.Count).Take(15).ToList(),
            Top(visits.Select(v => v.Path)),
            Top(visits.Select(v => v.Referrer)),
            Top(visits.Select(v => v.Device)),
            Top(visits.Select(v => v.Browser)),
            visits.OrderByDescending(v => v.At).Take(200)
                .Select(v => new RecentVisit(v.At, v.Path, v.Referrer, v.Ip, v.Country, v.City, v.Device, v.Browser, v.Os, v.Language))
                .ToList());
    }

    public async Task<int> CleanUpAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var ipBefore = now - KeepIp;
        var deleteBefore = now - KeepVisits;
        var old = await db.PageVisits.Where(v => v.At < deleteBefore || (v.Ip != null && v.At < ipBefore))
            .Take(5000).ToListAsync(cancellationToken);
        foreach (var visit in old)
        {
            if (visit.At < deleteBefore)
            {
                db.PageVisits.Remove(visit);
            }
            else
            {
                visit.Ip = null;
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        return old.Count;
    }

    private string Hash(string value) =>
        Convert.ToHexString(HMACSHA256.HashData(hashKey, Encoding.UTF8.GetBytes(value)))[..32];

    /// <summary>"/tariffs?x=1#y" → "/tariffs"; null when it is not a path of this site.</summary>
    private static string? CleanPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path[0] != '/' || path.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }
        var clean = path.Split('?', '#')[0];
        return clean.Length > 300 ? clean[..300] : clean;
    }

    /// <summary>Only another site's host name ("www.google.com"); none for links within this site.</summary>
    private static string? ReferrerHost(string? referrer, string? ownHost)
    {
        if (!Uri.TryCreate(referrer, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return null;
        }
        var host = uri.Host.ToLowerInvariant();
        return ownHost is not null && host.Equals(ownHost, StringComparison.OrdinalIgnoreCase) ? null
            : host.Length > 200 ? host[..200] : host;
    }

    private static bool IsLocal(IPAddress ip) =>
        IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal
        || ip.ToString() is var s && (s.StartsWith("10.") || s.StartsWith("192.168.") || s.StartsWith("172."));

    private static bool IsBot(string? ua) =>
        string.IsNullOrEmpty(ua) || BotPattern().IsMatch(ua);

    private static string Device(string? ua) =>
        ua is null ? "desktop"
        : ua.Contains("iPad") || (ua.Contains("Android") && !ua.Contains("Mobile")) || ua.Contains("Tablet") ? "tablet"
        : ua.Contains("Mobi") || ua.Contains("iPhone") || ua.Contains("Android") ? "mobile"
        : "desktop";

    private static string? Browser(string? ua) => ua switch
    {
        null => null,
        _ when ua.Contains("Edg/") => "Edge",
        _ when ua.Contains("OPR/") || ua.Contains("Opera") => "Opera",
        _ when ua.Contains("SamsungBrowser") => "Samsung Internet",
        _ when ua.Contains("Firefox/") || ua.Contains("FxiOS") => "Firefox",
        _ when ua.Contains("Chrome/") || ua.Contains("CriOS") => "Chrome",
        _ when ua.Contains("Safari/") => "Safari",
        _ => "Other",
    };

    private static string? Os(string? ua) => ua switch
    {
        null => null,
        _ when ua.Contains("iPhone") || ua.Contains("iPad") => "iOS",
        _ when ua.Contains("Android") => "Android",
        _ when ua.Contains("Windows") => "Windows",
        _ when ua.Contains("Mac OS X") || ua.Contains("Macintosh") => "macOS",
        _ when ua.Contains("Linux") => "Linux",
        _ => "Other",
    };

    [GeneratedRegex("bot|crawl|spider|slurp|preview|headless|lighthouse|monitor|curl|wget|python|http-client|facebookexternalhit|embedly", RegexOptions.IgnoreCase)]
    private static partial Regex BotPattern();
}
