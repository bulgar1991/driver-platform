using System.ComponentModel.DataAnnotations;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Dispatch;

public sealed record DriverDocumentDto
{
    public Guid? Id { get; init; }
    public DriverDocumentType Type { get; init; }
    [MaxLength(64)]
    public string? Number { get; init; }
    public DateOnly? ExpiresOn { get; init; }
}

public sealed record DriverRequest
{
    [Required, MaxLength(100)]
    public required string FirstName { get; init; }
    [Required, MaxLength(100)]
    public required string LastName { get; init; }
    [Required, Phone, MaxLength(32)]
    public required string Phone { get; init; }
    [EmailAddress, MaxLength(254)]
    public string? Email { get; init; }
    [MaxLength(10)]
    public List<string> Languages { get; init; } = [];
    [MaxLength(20)]
    public List<string> PreferredAreas { get; init; } = [];
    [Range(0, 100)]
    public decimal CommissionPercent { get; init; }
    public bool IsActive { get; init; } = true;
    public bool IsAvailable { get; init; } = true;
    [MaxLength(2000)]
    public string? Notes { get; init; }
    [MaxLength(20)]
    public List<DriverDocumentDto> Documents { get; init; } = [];

    [MaxLength(20)]
    public string? EmployeeNumber { get; init; }
    [MaxLength(32)]
    public string? TaxCode { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    [MaxLength(100)]
    public string? PlaceOfBirth { get; init; }
    public DateOnly? HiredOn { get; init; }
    public DateOnly? LeftOn { get; init; }
    public DriverContractType ContractType { get; init; }
    [Range(0, 24)]
    public decimal? MaxDrivingHours { get; init; }
    [Range(0, 10_000)]
    public decimal? HourlyCost { get; init; }
    [MaxLength(60)]
    public string? JobFunction { get; init; }
    public bool ExcludeFromAttendance { get; init; }
    [MaxLength(200)]
    public string? Address { get; init; }
    [Phone, MaxLength(32)]
    public string? HomePhone { get; init; }
    public DriverNotify NotifyBy { get; init; }
}

public sealed record DriverDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Phone,
    string? Email,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> PreferredAreas,
    decimal CommissionPercent,
    bool IsActive,
    bool IsAvailable,
    string? Notes,
    /// <summary>Picture at GET /api/photos/{id}; null when there is none.</summary>
    Guid? PhotoId,
    IReadOnlyList<DriverDocumentDto> Documents,
    /// <summary>Documents expired or expiring within 30 days.</summary>
    int DocumentAlerts,
    /// <summary>Average client rating (1–5); null until the first review.</summary>
    decimal? Rating,
    int RatingCount,
    int CompletedRides,
    int CancelledShifts,
    string? EmployeeNumber,
    string? TaxCode,
    DateOnly? DateOfBirth,
    string? PlaceOfBirth,
    DateOnly? HiredOn,
    DateOnly? LeftOn,
    DriverContractType ContractType,
    decimal? MaxDrivingHours,
    decimal? HourlyCost,
    string? JobFunction,
    bool ExcludeFromAttendance,
    string? Address,
    string? HomePhone,
    DriverNotify NotifyBy);

public sealed record DriverLinkResponse(string Url);

/// <summary>One booking on a driver's shift, for the ride history.</summary>
public sealed record DriverRide(
    Guid BookingId,
    string Reference,
    /// <summary>Local time at the pickup.</summary>
    DateTime PickupAt,
    string PickupAddress,
    string DropoffAddress,
    decimal DistanceKm,
    int DurationMinutes,
    BookingStatus Status,
    string VehiclePlate,
    string VehicleName,
    decimal TotalPrice,
    string Currency,
    /// <summary>Null when not set yet, or for dispatchers (pay is for admins).</summary>
    decimal? DriverPay);

/// <summary>A driver's rides in one month; the totals count completed rides only.</summary>
public sealed record DriverRidesResponse(
    int Year,
    int Month,
    IReadOnlyList<DriverRide> Rides,
    int CompletedRides,
    decimal CompletedKm,
    /// <summary>Salary for the month: pay of the completed rides. Null for dispatchers.</summary>
    decimal? PayTotal,
    /// <summary>Completed rides with no pay typed yet. Null for dispatchers.</summary>
    int? RidesWithoutPay);

public sealed record DriverPayRequest
{
    /// <summary>Null clears it.</summary>
    [Range(0, 10_000)]
    public decimal? Amount { get; init; }
}

/// <summary>One driver's line in the monthly salary list.</summary>
public sealed record DriverPayLine(
    Guid DriverId,
    string DriverName,
    int CompletedRides,
    decimal CompletedKm,
    decimal PayTotal,
    int RidesWithoutPay);

public sealed record VehicleRequest
{
    [Required, MaxLength(16), RegularExpression("^[A-Za-z0-9 -]{4,16}$")]
    public required string PlateNumber { get; init; }
    [Required, MaxLength(60)]
    public required string Make { get; init; }
    [Required, MaxLength(60)]
    public required string Model { get; init; }
    [MaxLength(40)]
    public string? Color { get; init; }
    public VehicleCategory Category { get; init; }
    [Range(1, 50)]
    public int Seats { get; init; }
    [Range(0, 60)]
    public int LuggageCapacity { get; init; }
    [Range(0, 4)]
    public int ChildSeatsAvailable { get; init; }
    public DateOnly? InsuranceExpiresOn { get; init; }
    public DateOnly? InspectionExpiresOn { get; init; }
    public DateOnly? NextServiceOn { get; init; }
    [Range(0, 2_000_000)]
    public int MileageKm { get; init; }
    public bool IsActive { get; init; } = true;
    [MaxLength(2000)]
    public string? Notes { get; init; }

    [MaxLength(20)]
    public string? Code { get; init; }
    public DateOnly? RegisteredOn { get; init; }
    [MaxLength(32)]
    public string? ChassisNumber { get; init; }
    [Range(0, 100)]
    public decimal? CostPerKm { get; init; }
    public Guid? DefaultDriverId { get; init; }
    [MaxLength(100)]
    public string? LeasingCompany { get; init; }
    public bool HasDvd { get; init; }
    public bool HasWifi { get; init; }
    public bool HasWc { get; init; }
    public bool AutomaticGearbox { get; init; }
    public bool WheelchairRamp { get; init; }
}

public sealed record VehicleDto(
    Guid Id,
    string PlateNumber,
    string Make,
    string Model,
    string? Color,
    VehicleCategory Category,
    int Seats,
    int LuggageCapacity,
    int ChildSeatsAvailable,
    DateOnly? InsuranceExpiresOn,
    DateOnly? InspectionExpiresOn,
    DateOnly? NextServiceOn,
    int MileageKm,
    bool IsActive,
    string? Notes,
    /// <summary>Picture at GET /api/photos/{id}; null when there is none.</summary>
    Guid? PhotoId,
    int DocumentAlerts,
    string? Code,
    DateOnly? RegisteredOn,
    string? ChassisNumber,
    decimal? CostPerKm,
    Guid? DefaultDriverId,
    string? DefaultDriverName,
    string? LeasingCompany,
    bool HasDvd,
    bool HasWifi,
    bool HasWc,
    bool AutomaticGearbox,
    bool WheelchairRamp);
