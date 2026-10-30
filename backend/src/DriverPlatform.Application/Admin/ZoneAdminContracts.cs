using System.ComponentModel.DataAnnotations;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;

namespace DriverPlatform.Application.Admin;

public sealed record ZoneAdminDto(
    Guid Id,
    string Name,
    Translations NameTranslations,
    ZoneType Type,
    ZoneShape Shape,
    double? CenterLatitude,
    double? CenterLongitude,
    double? RadiusMeters,
    IReadOnlyList<PolygonPoint> Polygon,
    decimal AccessFee,
    ZonePricing Pricing,
    IReadOnlyList<ZoneRate> Rates,
    decimal? FixedPriceMaxKm,
    IReadOnlyList<MeetingPoint> MeetingPoints,
    bool IsActive);

public sealed record ZoneRequest
{
    [Required, MaxLength(120)]
    public required string Name { get; init; }
    /// <summary>The name clients see in Italian, German and French; blank ones show the English name.</summary>
    public Translations? NameTranslations { get; init; }
    public ZoneType Type { get; init; }
    public ZoneShape Shape { get; init; }
    public double? CenterLatitude { get; init; }
    public double? CenterLongitude { get; init; }
    [Range(50, 50_000)]
    public double? RadiusMeters { get; init; }
    [MaxLength(200)]
    public List<PolygonPoint> Polygon { get; init; } = [];
    [Range(0, 500)]
    public decimal AccessFee { get; init; }
    public ZonePricing Pricing { get; init; }
    /// <summary>One per vehicle category at most; categories left out use the normal rates.</summary>
    [MaxLength(10)]
    public List<ZoneRate> Rates { get; init; } = [];
    [Range(1, 2000)]
    public decimal? FixedPriceMaxKm { get; init; }
    [MaxLength(20)]
    public List<MeetingPoint> MeetingPoints { get; init; } = [];
    public bool IsActive { get; init; } = true;
}
