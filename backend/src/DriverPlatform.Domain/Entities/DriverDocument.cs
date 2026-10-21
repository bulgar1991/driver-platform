using DriverPlatform.Domain.Common;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Domain.Entities;

public class DriverDocument : Entity
{
    public Guid DriverId { get; set; }
    public Driver Driver { get; set; } = null!;
    public DriverDocumentType Type { get; set; }
    public string? Number { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public string? FilePath { get; set; }
}
