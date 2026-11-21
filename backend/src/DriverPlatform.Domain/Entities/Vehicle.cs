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

    /// <summary>Internal fleet code ("T1", "Van 2").</summary>
    public string? Code { get; set; }
    public DateOnly? RegisteredOn { get; set; }
    public string? ChassisNumber { get; set; }
    public decimal? CostPerKm { get; set; }
    /// <summary>Driver who usually drives it; suggested first when building a shift.</summary>
    public Guid? DefaultDriverId { get; set; }
    /// <summary>Leasing or rental company, when the car is not owned.</summary>
    public string? LeasingCompany { get; set; }
    public bool HasDvd { get; set; }
    public bool HasWifi { get; set; }
    public bool HasWc { get; set; }
    public bool AutomaticGearbox { get; set; }
    public bool WheelchairRamp { get; set; }

    public ICollection<VehicleExpiration> Expirations { get; set; } = [];
    public ICollection<VehicleRefuel> Refuels { get; set; } = [];
}
