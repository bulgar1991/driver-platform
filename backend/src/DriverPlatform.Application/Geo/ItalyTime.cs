namespace DriverPlatform.Application.Geo;

/// <summary>Pickup times are entered and shown in Italian local time, whatever the client's own time zone.</summary>
public static class ItalyTime
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    /// <summary>Null when the local time does not exist (the hour skipped when clocks go forward).</summary>
    public static DateTimeOffset? ToUtc(DateTime local)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (Zone.IsInvalidTime(local))
        {
            return null;
        }
        return new DateTimeOffset(local, Zone.GetUtcOffset(local)).ToUniversalTime();
    }

    public static DateTime ToLocal(DateTimeOffset utc) =>
        DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(utc, Zone).DateTime, DateTimeKind.Unspecified);
}
