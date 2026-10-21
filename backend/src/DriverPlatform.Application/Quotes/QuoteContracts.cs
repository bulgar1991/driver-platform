using System.ComponentModel.DataAnnotations;
using DriverPlatform.Application.Pricing;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;

namespace DriverPlatform.Application.Quotes;

public sealed record PointInput
{
    [Required, MaxLength(300)]
    public required string Address { get; init; }
    [Range(-90, 90)]
    public double Latitude { get; init; }
    [Range(-180, 180)]
    public double Longitude { get; init; }
}

public sealed record ChildSeatInput
{
    public ChildSeatGroup Group { get; init; }
    [Range(1, 4)]
    public int Count { get; init; }
}

public sealed record ReturnLegInput
{
    /// <summary>Italian local time, without an offset.</summary>
    public DateTime PickupAt { get; init; }
    public bool NameSign { get; init; }
    [MaxLength(16)]
    public string? FlightNumber { get; init; }
}

public sealed record QuoteRequest
{
    [Required]
    public required PointInput Pickup { get; init; }
    [Required]
    public required PointInput Dropoff { get; init; }
    /// <summary>Italian local time, without an offset, e.g. "2026-11-02T14:30:00".</summary>
    public DateTime PickupAt { get; init; }
    [Range(1, 8)]
    public int Passengers { get; init; } = 1;
    [Range(0, 16)]
    public int LargeLuggage { get; init; }
    [Range(0, 16)]
    public int SmallLuggage { get; init; }
    [MaxLength(3)]
    public List<ChildSeatInput> ChildSeats { get; init; } = [];
    public bool NameSign { get; init; }
    /// <summary>Name of the chosen meeting point when the pickup is in a zone that has them; the nearest one otherwise.</summary>
    [MaxLength(120)]
    public string? PickupMeetingPoint { get; init; }
    [MaxLength(120)]
    public string? DropoffMeetingPoint { get; init; }
    public ReturnLegInput? Return { get; init; }
}

public sealed record ZoneDto(
    Guid Id,
    string Name,
    ZoneType Type,
    ZoneShape Shape,
    double? CenterLatitude,
    double? CenterLongitude,
    double? RadiusMeters,
    IReadOnlyList<PolygonPoint> Polygon,
    decimal AccessFee,
    IReadOnlyList<MeetingPoint> MeetingPoints);

public sealed record SurchargeDto(string Name, decimal Percent);

public sealed record QuoteLeg(
    PointInput Pickup,
    PointInput Dropoff,
    DateTime PickupAt,
    DateTimeOffset PickupAtUtc,
    ZoneDto? PickupZone,
    ZoneDto? DropoffZone,
    MeetingPoint? PickupMeetingPoint,
    MeetingPoint? DropoffMeetingPoint,
    decimal DistanceKm,
    int DurationMinutes,
    bool RouteIsEstimate,
    IReadOnlyList<double[]> Path,
    IReadOnlyList<SurchargeDto> Surcharges,
    decimal SurchargePercent);

public sealed record VehicleOption(
    VehicleCategory Category,
    int MaxPassengers,
    int MaxLuggage,
    bool Available,
    string? UnavailableReason,
    decimal Total,
    decimal VatIncluded,
    LegPrice Outbound,
    LegPrice? Return);

public sealed record QuoteResponse(
    QuoteLeg Outbound,
    QuoteLeg? Return,
    IReadOnlyList<VehicleOption> Vehicles,
    string Currency,
    decimal VatRatePercent);

/// <summary>Capacity and public tariff of a vehicle class (price = base fee + km × price per km, before surcharges).</summary>
public sealed record VehicleCapacity(VehicleCategory Category, int MaxPassengers, int MaxLuggage, decimal BaseFee, decimal PricePerKm);

/// <summary>An active surcharge as shown on the public tariffs; public holidays are grouped into one line.</summary>
public sealed record PublicSurcharge(string Name, decimal Percent);

/// <summary>What the booking form needs before the first quote.</summary>
public sealed record BookingOptionsResponse(
    IReadOnlyList<VehicleCapacity> Vehicles,
    IReadOnlyDictionary<ExtraType, decimal> Extras,
    decimal ReturnDiscountPercent,
    int MinLeadTimeHours,
    string Currency,
    IReadOnlyList<PublicSurcharge> Surcharges);
