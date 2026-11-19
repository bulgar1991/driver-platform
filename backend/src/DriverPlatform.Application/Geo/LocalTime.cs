namespace DriverPlatform.Application.Geo;

/// <summary>Wall-clock time in a given time zone, to and from UTC.</summary>
public static class LocalTime
{
    /// <summary>Null when the local time does not exist (the hour skipped when clocks go forward).</summary>
    public static DateTimeOffset? ToUtc(DateTime local, TimeZoneInfo zone)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            return null;
        }
        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }

    public static DateTime ToLocal(DateTimeOffset utc, TimeZoneInfo zone) =>
        DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(utc, zone).DateTime, DateTimeKind.Unspecified);
}
