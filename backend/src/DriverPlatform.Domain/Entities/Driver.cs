using DriverPlatform.Domain.Common;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Domain.Entities;

public class Driver : Entity
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public List<string> Languages { get; set; } = [];
    public List<string> PreferredAreas { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public bool IsAvailable { get; set; } = true;
    /// <summary>SHA-256 of the driver's personal link token; the token itself is only sent to the driver.</summary>
    public string? AccessTokenHash { get; set; }
    /// <summary>Raised when the dispatcher regenerates the link, which invalidates the old one.</summary>
    public int LinkVersion { get; set; }
    public decimal CommissionPercent { get; set; }
    public string? Notes { get; set; }
    public Guid? PhotoId { get; set; }

    // Personnel record (staff card).
    public string? EmployeeNumber { get; set; }
    /// <summary>Tax code or social security number (Italian codice fiscale, Swiss AHV number).</summary>
    public string? TaxCode { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? PlaceOfBirth { get; set; }
    public DateOnly? HiredOn { get; set; }
    /// <summary>Last working day; after it the driver is no longer counted or offered for shifts.</summary>
    public DateOnly? LeftOn { get; set; }
    public DriverContractType ContractType { get; set; }
    /// <summary>Most driving hours allowed per day.</summary>
    public decimal? MaxDrivingHours { get; set; }
    public decimal? HourlyCost { get; set; }
    /// <summary>"Driver", "Chauffeur", "Mechanic"…: free text, used to filter the attendance calendar.</summary>
    public string? JobFunction { get; set; }
    /// <summary>Left out of the attendance calendar (e.g. an occasional or office worker).</summary>
    public bool ExcludeFromAttendance { get; set; }
    public string? Address { get; set; }
    public string? HomePhone { get; set; }
    public DriverNotify NotifyBy { get; set; }

    public ICollection<DriverDocument> Documents { get; set; } = [];
    public ICollection<Assignment> Assignments { get; set; } = [];
}
