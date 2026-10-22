using System.ComponentModel.DataAnnotations;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;

namespace DriverPlatform.Application.Admin;

public sealed record VehicleRateDto(VehicleCategory Category, decimal BaseFee, decimal PricePerKm, int MaxPassengers, int MaxLuggage);

public sealed record UpdateVehicleRateRequest
{
    [Range(0, 1000)]
    public decimal BaseFee { get; init; }
    [Range(0, 100)]
    public decimal PricePerKm { get; init; }
    [Range(1, 8)]
    public int MaxPassengers { get; init; }
    [Range(0, 16)]
    public int MaxLuggage { get; init; }
}

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
