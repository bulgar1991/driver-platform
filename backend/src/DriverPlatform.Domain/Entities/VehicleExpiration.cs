using DriverPlatform.Domain.Common;

namespace DriverPlatform.Domain.Entities;

/// <summary>A deadline of a vehicle besides insurance and inspection: road tax, tachograph, service, fire extinguisher…</summary>
public class VehicleExpiration : Entity
{
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public string Title { get; set; } = "";
    public DateOnly? DueOn { get; set; }
    /// <summary>Due at this odometer reading (e.g. a service every 30,000 km).</summary>
    public int? DueKm { get; set; }
    public string? Notes { get; set; }
}
