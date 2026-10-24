using DriverPlatform.Domain.Common;

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

    public ICollection<DriverDocument> Documents { get; set; } = [];
    public ICollection<Assignment> Assignments { get; set; } = [];
}
