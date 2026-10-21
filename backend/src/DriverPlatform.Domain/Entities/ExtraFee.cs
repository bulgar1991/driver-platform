using DriverPlatform.Domain.Common;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Domain.Entities;

/// <summary>Fixed amount for options such as a child seat or a name sign.</summary>
public class ExtraFee : Entity
{
    public ExtraType Type { get; set; }
    public decimal Amount { get; set; }
}
