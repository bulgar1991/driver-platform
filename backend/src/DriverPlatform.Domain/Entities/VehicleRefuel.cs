using DriverPlatform.Domain.Common;

namespace DriverPlatform.Domain.Entities;

/// <summary>A refuelling with the odometer reading at that moment; the readings give the mileage and the consumption.</summary>
public class VehicleRefuel : Entity
{
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public DateOnly Date { get; set; }
    public int OdometerKm { get; set; }
    public decimal Liters { get; set; }
    public decimal? Cost { get; set; }
    /// <summary>Filling station or company tank.</summary>
    public string? Station { get; set; }
    public Guid? DriverId { get; set; }
    public Driver? Driver { get; set; }
}
