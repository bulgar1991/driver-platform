using DriverPlatform.Domain.Common;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;

namespace DriverPlatform.Domain.Entities;

/// <summary>A percentage increase by day, date range or time of day (weekend, holidays, night, peak).</summary>
public class SurchargeRule : Entity
{
    /// <summary>In English, as typed in the admin panel; clients see <see cref="NameTranslations"/> in their language.</summary>
    public string Name { get; set; } = "";
    public Translations? NameTranslations { get; set; }
    public SurchargeKind Kind { get; set; }
    public decimal Percent { get; set; }
    public List<DayOfWeek> DaysOfWeek { get; set; } = [];
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    /// <summary>For date ranges: ignore the year, so the rule applies every year (fixed holidays, July–August).</summary>
    public bool RepeatsYearly { get; set; }
    public bool IsActive { get; set; } = true;
}
