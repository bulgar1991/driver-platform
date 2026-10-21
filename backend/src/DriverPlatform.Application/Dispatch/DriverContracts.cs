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
    IReadOnlyList<DriverDocumentDto> Documents,
    /// <summary>Documents expired or expiring within 30 days.</summary>
    int DocumentAlerts,
    /// <summary>Average client rating (1–5); null until the first review.</summary>
    decimal? Rating,
    int RatingCount,
    int CompletedRides,
    int CancelledShifts);

public sealed record DriverLinkResponse(string Url);

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
    [Range(1, 8)]
    public int Seats { get; init; }
    [Range(0, 16)]
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
    int DocumentAlerts);
