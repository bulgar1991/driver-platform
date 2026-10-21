using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Pricing;

public static class SurchargeMatcher
{
    /// <param name="localPickup">Pickup time in Italian local time.</param>
    public static bool Matches(SurchargeRule rule, DateTime localPickup)
    {
        if (!rule.IsActive)
        {
            return false;
        }

        return rule.Kind switch
        {
            SurchargeKind.DaysOfWeek => rule.DaysOfWeek.Contains(localPickup.DayOfWeek),
            SurchargeKind.DateRange => MatchesDates(rule, DateOnly.FromDateTime(localPickup)),
            SurchargeKind.TimeOfDay => MatchesTime(rule, TimeOnly.FromDateTime(localPickup)),
            _ => false,
        };
    }

    private static bool MatchesDates(SurchargeRule rule, DateOnly date)
    {
        if (rule is not { StartDate: { } start, EndDate: { } end })
        {
            return false;
        }
        if (!rule.RepeatsYearly)
        {
            return date >= start && date <= end;
        }

        // Compare month and day only; a range such as 20 Dec – 6 Jan wraps over the new year.
        var (day, from, to) = (MonthDay(date), MonthDay(start), MonthDay(end));
        return from <= to ? day >= from && day <= to : day >= from || day <= to;
    }

    private static bool MatchesTime(SurchargeRule rule, TimeOnly time)
    {
        if (rule is not { StartTime: { } start, EndTime: { } end })
        {
            return false;
        }
        // The end is exclusive; 22:00–06:00 wraps over midnight.
        return start <= end ? time >= start && time < end : time >= start || time < end;
    }

    private static int MonthDay(DateOnly d) => d.Month * 100 + d.Day;
}
