using DriverPlatform.Domain.Common;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Domain.Entities;

public class Vehicle : Entity
{
    public string PlateNumber { get; set; } = "";
    public string Make { get; set; } = "";
    public string Model { get; set; } = "";
    public string? Color { get; set; }
    public VehicleCategory Category { get; set; }
    public int Seats { get; set; }
    public int LuggageCapacity { get; set; }
    public int ChildSeatsAvailable { get; set; }
    public DateOnly? InsuranceExpiresOn { get; set; }
    /// <summary>Italian "revisione" (technical inspection).</summary>
    public DateOnly? InspectionExpiresOn { get; set; }
    public DateOnly? NextServiceOn { get; set; }
    public int MileageKm { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public Guid? PhotoId { get; set; }
}
