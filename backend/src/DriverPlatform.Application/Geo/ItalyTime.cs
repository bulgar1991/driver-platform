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

    /// <summary>Start and end (exclusive) of an Italian calendar month. Midnight always exists: clocks change at 2–3 am.</summary>
    public static (DateTimeOffset From, DateTimeOffset To) Month(int year, int month)
    {
        var first = new DateTime(year, month, 1);
        return (ToUtc(first)!.Value, ToUtc(first.AddMonths(1))!.Value);
    }

    public static DateTime ToLocal(DateTimeOffset utc) =>
        DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(utc, Zone).DateTime, DateTimeKind.Unspecified);
}
