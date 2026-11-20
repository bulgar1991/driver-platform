using DriverPlatform.Domain.Common;

namespace DriverPlatform.Domain.Entities;

/// <summary>
/// One page seen on the public website (Admin → Visitors). The IP is cleared after 30 days and the
/// whole row deleted after 13 months; the visitor hash (IP + browser, salted) only counts unique
/// visitors and cannot be turned back into an IP.
/// </summary>
public class PageVisit : Entity
{
    public DateTimeOffset At { get; set; }
    public string Path { get; set; } = "";
    /// <summary>The site the visitor came from (host only), null when typed in or unknown.</summary>
    public string? Referrer { get; set; }
    public string? Ip { get; set; }
    /// <summary>ISO country code, from the IP.</summary>
    public string? Country { get; set; }
    public string? City { get; set; }
    /// <summary>"desktop", "mobile" or "tablet".</summary>
    public string Device { get; set; } = "desktop";
    public string? Browser { get; set; }
    public string? Os { get; set; }
    /// <summary>The site language the page was seen in ("en", "it"…).</summary>
    public string? Language { get; set; }
    public string VisitorHash { get; set; } = "";
}
