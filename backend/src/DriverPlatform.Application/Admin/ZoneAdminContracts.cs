using System.ComponentModel.DataAnnotations;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;

namespace DriverPlatform.Application.Admin;

public sealed record ZoneAdminDto(
    Guid Id,
    string Name,
    ZoneType Type,
    ZoneShape Shape,
    double? CenterLatitude,
    double? CenterLongitude,
    double? RadiusMeters,
    IReadOnlyList<PolygonPoint> Polygon,
    decimal AccessFee,
    IReadOnlyList<MeetingPoint> MeetingPoints,
    bool IsActive);

public sealed record ZoneRequest
{
    [Required, MaxLength(120)]
    public required string Name { get; init; }
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
    [MaxLength(20)]
    public List<MeetingPoint> MeetingPoints { get; init; } = [];
    public bool IsActive { get; init; } = true;
}
