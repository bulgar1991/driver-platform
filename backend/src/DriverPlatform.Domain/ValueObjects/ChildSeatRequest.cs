using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Domain.ValueObjects;

public sealed class ChildSeatRequest
{
    public ChildSeatGroup Group { get; set; }
    public int Count { get; set; }
}
