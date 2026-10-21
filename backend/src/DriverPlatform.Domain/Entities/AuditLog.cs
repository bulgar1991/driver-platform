namespace DriverPlatform.Domain.Entities;

/// <summary>Who changed what, when and from which IP. Append-only.</summary>
public class AuditLog
{
    public long Id { get; set; }
    public Guid? StaffUserId { get; set; }
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string? EntityId { get; set; }
    public string? Changes { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
