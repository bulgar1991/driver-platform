using DriverPlatform.Application.Common;
using DriverPlatform.Application.Localization;

namespace DriverPlatform.Application.Bookings;

/// <summary>Flight takeoff and landing, as on the ticket: each airport's local time, not Italian time.</summary>
public static class FlightTimes
{
    /// <summary>
    /// Wide enough for airports far from Italy's time zone and for a ride hours before takeoff,
    /// narrow enough to catch a wrong day or month.
    /// </summary>
    private static readonly TimeSpan MaxFromPickup = TimeSpan.FromDays(2);

    /// <summary>The time to the minute, without a time zone; refused when far from the pickup (Italian local time).</summary>
    public static DateTime? Check(DateTime? time, DateTime pickupLocal, string field)
    {
        if (time is not { } t)
        {
            return null;
        }
        var local = new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, DateTimeKind.Unspecified);
        if ((local - pickupLocal).Duration() > MaxFromPickup)
        {
            throw new RequestValidationException(field, Texts.Current.FlightTimeNearPickup);
        }
        return local;
    }
}
