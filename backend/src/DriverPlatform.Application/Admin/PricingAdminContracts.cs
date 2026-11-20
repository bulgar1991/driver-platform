using System.ComponentModel.DataAnnotations;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;

namespace DriverPlatform.Application.Admin;

/// <summary>A tariff in the panel. Names: empty ones show the built-in name (Standard, Business, VIP, Bus).</summary>
public sealed record VehicleRateDto(
    VehicleCategory Category,
    decimal BaseFee,
    decimal PricePerKm,
    int MaxPassengers,
    int MaxLuggage,
    VehicleClassNames Names,
    bool IsBuiltIn,
    bool IsActive,
    int SortOrder,
    bool InUse);

/// <summary>A tariff's name in the site's languages.</summary>
public sealed record VehicleClassNames(
    [MaxLength(60)] string? En,
    [MaxLength(60)] string? It,
    [MaxLength(60)] string? De,
    [MaxLength(60)] string? Fr);

/// <summary>A vehicle class for the staff panel (cars, zones): all of them, hidden ones too.</summary>
public sealed record VehicleClassDto(VehicleCategory Category, VehicleClassNames Names, bool IsBuiltIn, bool IsActive, int MaxPassengers, int MaxLuggage);

public record UpdateVehicleRateRequest
{
    [Range(0, 1000)]
    public decimal BaseFee { get; init; }
    [Range(0, 100)]
    public decimal PricePerKm { get; init; }
    [Range(1, 50)]
    public int MaxPassengers { get; init; }
    [Range(0, 60)]
    public int MaxLuggage { get; init; }
    /// <summary>Optional for the built-in tariffs; null keeps the names as they are.</summary>
    public VehicleClassNames? Names { get; init; }
}

/// <summary>A new tariff: the English name is required (the others fall back to it).</summary>
public sealed record CreateVehicleRateRequest : UpdateVehicleRateRequest;

public sealed record VehicleRateActiveRequest(bool IsActive);

/// <summary>What deleting did: removed for good, or hidden because bookings or cars still use it.</summary>
public sealed record DeleteVehicleRateResult(bool Deleted, bool Hidden, IReadOnlyList<VehicleRateDto> Rates);

public sealed record SurchargeRuleDto(
    Guid Id,
    string Name,
    Translations NameTranslations,
    SurchargeKind Kind,
    decimal Percent,
    IReadOnlyList<DayOfWeek> DaysOfWeek,
    DateOnly? StartDate,
    DateOnly? EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool RepeatsYearly,
    bool IsActive);

public sealed record SurchargeRuleRequest
{
    [Required, MaxLength(120)]
    public required string Name { get; init; }
    /// <summary>The name clients see in Italian, German and French; blank ones show the English name.</summary>
    public Translations? NameTranslations { get; init; }
    public SurchargeKind Kind { get; init; }
    [Range(0, 200)]
    public decimal Percent { get; init; }
    public List<DayOfWeek> DaysOfWeek { get; init; } = [];
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public TimeOnly? StartTime { get; init; }
    public TimeOnly? EndTime { get; init; }
    public bool RepeatsYearly { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record ExtraFeeDto(ExtraType Type, decimal Amount);

public sealed record UpdateExtraFeeRequest
{
    [Range(0, 500)]
    public decimal Amount { get; init; }
}

public sealed record PricingSettingsDto
{
    [Range(0, 50)]
    public decimal VatRatePercent { get; init; }
    [Range(0, 50)]
    public decimal ReturnDiscountPercent { get; init; }
    [Range(0, 168)]
    public int MinLeadTimeHours { get; init; }
}

public sealed record PricingOverview(
    IReadOnlyList<VehicleRateDto> Rates,
    IReadOnlyList<SurchargeRuleDto> Surcharges,
    IReadOnlyList<ExtraFeeDto> Extras,
    PricingSettingsDto Settings);
