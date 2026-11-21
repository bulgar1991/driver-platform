using System.ComponentModel.DataAnnotations;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Search;

/// <summary>How the ride was paid: online and kept, refunded, or not online at all (partners, invoiced).</summary>
public enum PaymentFilter
{
    Any,
    Paid,
    Refunded,
    NotPaidOnline,
}

/// <summary>Which PDF report to make; each needs its own filter (see <see cref="SearchService.AvailableReports"/>).</summary>
public enum ReportKind
{
    /// <summary>Every ride found, one line each.</summary>
    List,
    /// <summary>One day: the rides of each driver, in order, for the morning briefing.</summary>
    DailySheet,
    /// <summary>One driver: rides, kilometres and (for admins) pay.</summary>
    Driver,
    /// <summary>One partner: rides and amounts, to check before the monthly invoice.</summary>
    Partner,
    /// <summary>One vehicle: rides and kilometres.</summary>
    Vehicle,
}

public sealed record SearchQuery
{
    /// <summary>Office dates of the pickup, inclusive; at most 366 days.</summary>
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    [MaxLength(100)]
    public string? Text { get; init; }
    [MaxLength(6)]
    public List<BookingStatus> Statuses { get; init; } = [];
    public BookingSource? Source { get; init; }
    public Guid? PartnerId { get; init; }
    public Guid? DriverId { get; init; }
    public Guid? VehicleId { get; init; }
    public VehicleCategory? Category { get; init; }
    public PaymentFilter Payment { get; init; }
    /// <summary>Only rides not in a shift yet.</summary>
    public bool Unassigned { get; init; }
    public bool IncludeCancelled { get; init; }
}

/// <param name="PickupLocal">Local time at the pickup.</param>
/// <param name="DriverPay">Admins only.</param>
public sealed record SearchRide(
    Guid Id,
    string Reference,
    BookingStatus Status,
    BookingSource Source,
    DateTime PickupLocal,
    int DurationMinutes,
    string PickupLabel,
    string DropoffLabel,
    string ClientName,
    string ClientPhone,
    string? ClientEmail,
    string? PartnerName,
    string? ExternalReference,
    int Passengers,
    int LargeLuggage,
    int SmallLuggage,
    VehicleCategory VehicleCategory,
    decimal DistanceKm,
    decimal TotalPrice,
    decimal? DriverPay,
    PaymentStatus? PaymentStatus,
    string? FlightNumber,
    Guid? DriverId,
    string? DriverName,
    Guid? VehicleId,
    string? VehiclePlate,
    AssignmentStatus? ShiftStatus);

/// <summary>Totals of the rides found, cancelled ones left out.</summary>
public sealed record SearchTotals(int Rides, int Cancelled, decimal Revenue, decimal? DriverPay, decimal Km, int Passengers);

/// <param name="Truncated">More rides matched than were returned: narrow the search.</param>
/// <param name="Reports">The PDF reports these filters allow.</param>
public sealed record SearchResult(
    IReadOnlyList<SearchRide> Rides,
    bool Truncated,
    SearchTotals Totals,
    string Currency,
    IReadOnlyList<ReportKind> Reports);

public sealed record SearchOption(Guid Id, string Name, bool IsActive);

/// <summary>What the filters offer: partners, drivers and vehicles.</summary>
public sealed record SearchOptions(IReadOnlyList<SearchOption> Partners, IReadOnlyList<SearchOption> Drivers, IReadOnlyList<SearchOption> Vehicles);
