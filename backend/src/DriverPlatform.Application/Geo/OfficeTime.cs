namespace DriverPlatform.Application.Geo;

/// <summary>
/// The company's own time zone (Site:TimeZone, per server; Europe/Rome by default): "today" in the
/// panel, days of shifts, months of reports, statements and invoices. Ride times use the local time
/// where the ride starts instead (<see cref="RideTime"/>).
/// </summary>
public static class OfficeTime
{
    public const string DefaultZone = "Europe/Rome";

    public static TimeZoneInfo Zone { get; private set; } = TimeZoneInfo.FindSystemTimeZoneById(DefaultZone);

    /// <summary>Set once at startup from configuration. An unknown zone stops the app at once.</summary>
    public static void UseZone(string? id) =>
        Zone = TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(id) ? DefaultZone : id.Trim());

    /// <summary>Null when the local time does not exist (the hour skipped when clocks go forward).</summary>
    public static DateTimeOffset? ToUtc(DateTime local) => LocalTime.ToUtc(local, Zone);

    /// <summary>Start and end (exclusive) of a calendar month. Midnight always exists: clocks change at night, not at midnight, in Europe.</summary>
    public static (DateTimeOffset From, DateTimeOffset To) Month(int year, int month)
    {
        var first = new DateTime(year, month, 1);
        return (ToUtc(first)!.Value, ToUtc(first.AddMonths(1))!.Value);
    }

    public static DateTime ToLocal(DateTimeOffset utc) => LocalTime.ToLocal(utc, Zone);
}
