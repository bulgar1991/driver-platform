using DriverPlatform.Domain.Common;

namespace DriverPlatform.Domain.Entities;

/// <summary>A kind of attendance event (holiday, sick leave, training…), with its colour in the calendar. Editable in the panel.</summary>
public class AttendanceType : Entity
{
    public string Name { get; set; } = "";
    /// <summary>One to three letters shown in the calendar cell.</summary>
    public string Code { get; set; } = "";
    /// <summary>"#22c55e".</summary>
    public string Color { get; set; } = "";
    /// <summary>A whole day of this kind takes the driver off the "available" count and out of shifts.</summary>
    public bool MakesUnavailable { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>One day of one staff member in the attendance calendar. Without times it lasts the whole day.</summary>
public class AttendanceEntry : Entity
{
    public Guid DriverId { get; set; }
    public Driver Driver { get; set; } = null!;
    public Guid TypeId { get; set; }
    public AttendanceType Type { get; set; } = null!;
    public DateOnly Date { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? Notes { get; set; }
}
