using DriverPlatform.Application.Pricing;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Tests.Pricing;

public sealed class SurchargeMatcherTests
{
    private static readonly SurchargeRule Night = new()
    {
        Kind = SurchargeKind.TimeOfDay,
        StartTime = new TimeOnly(22, 0),
        EndTime = new TimeOnly(6, 0),
    };

    [Theory]
    [InlineData("2026-11-03T22:00", true)]
    [InlineData("2026-11-04T05:59", true)]
    [InlineData("2026-11-04T06:00", false)]
    [InlineData("2026-11-03T21:59", false)]
    public void Night_wraps_over_midnight(string local, bool expected) =>
        Assert.Equal(expected, SurchargeMatcher.Matches(Night, DateTime.Parse(local)));

    [Fact]
    public void Weekend_matches_friday_to_sunday()
    {
        var weekend = new SurchargeRule
        {
            Kind = SurchargeKind.DaysOfWeek,
            DaysOfWeek = [DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday],
        };

        Assert.True(SurchargeMatcher.Matches(weekend, new DateTime(2026, 11, 6, 12, 0, 0)));  // Friday
        Assert.False(SurchargeMatcher.Matches(weekend, new DateTime(2026, 11, 5, 12, 0, 0))); // Thursday
    }

    [Fact]
    public void Yearly_ranges_ignore_the_year_and_can_wrap_over_new_year()
    {
        var christmas = new SurchargeRule
        {
            Kind = SurchargeKind.DateRange,
            StartDate = new DateOnly(2026, 12, 20),
            EndDate = new DateOnly(2027, 1, 6),
            RepeatsYearly = true,
        };

        Assert.True(SurchargeMatcher.Matches(christmas, new DateTime(2030, 12, 31, 10, 0, 0)));
        Assert.True(SurchargeMatcher.Matches(christmas, new DateTime(2031, 1, 6, 23, 0, 0)));
        Assert.False(SurchargeMatcher.Matches(christmas, new DateTime(2031, 1, 7, 0, 0, 0)));
    }

    [Fact]
    public void One_off_ranges_keep_the_year_and_inactive_rules_never_match()
    {
        var easter = new SurchargeRule
        {
            Kind = SurchargeKind.DateRange,
            StartDate = new DateOnly(2027, 3, 29),
            EndDate = new DateOnly(2027, 3, 29),
        };

        Assert.True(SurchargeMatcher.Matches(easter, new DateTime(2027, 3, 29, 9, 0, 0)));
        Assert.False(SurchargeMatcher.Matches(easter, new DateTime(2028, 3, 29, 9, 0, 0)));

        easter.IsActive = false;
        Assert.False(SurchargeMatcher.Matches(easter, new DateTime(2027, 3, 29, 9, 0, 0)));
    }
}
