using DriverPlatform.Domain.Common;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Domain.Entities;

/// <summary>Price = BaseFee + km × PricePerKm, per vehicle category.</summary>
public class VehicleRate : Entity
{
    public VehicleCategory Category { get; set; }
    public decimal BaseFee { get; set; }
    public decimal PricePerKm { get; set; }
    public int MaxPassengers { get; set; }
    public int MaxLuggage { get; set; }
}
