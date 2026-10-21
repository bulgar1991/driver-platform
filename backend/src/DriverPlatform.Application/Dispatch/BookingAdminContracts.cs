using System.ComponentModel.DataAnnotations;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;

namespace DriverPlatform.Application.Dispatch;

public sealed record BookingQuery
{
    /// <summary>Italian dates, inclusive.</summary>
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public BookingStatus? Status { get; init; }
    /// <summary>Paid or partner bookings not in a shift yet.</summary>
    public bool Unassigned { get; init; }
    /// <summary>Also list bookings still waiting for the client's payment (hidden by default).</summary>
    public bool IncludeUnpaid { get; init; }
    [MaxLength(100)]
    public string? Search { get; init; }
    [Range(1, 10_000)]
    public int Page { get; init; } = 1;
    [Range(1, 200)]
    public int PageSize { get; init; } = 50;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record BookingListItem(
    Guid Id,
    string Reference,
    BookingStatus Status,
    BookingSource Source,
    DateTime PickupAt,
    string PickupLabel,
    string DropoffLabel,
    string ClientName,
    string ClientPhone,
    int Passengers,
    int LargeLuggage,
    int SmallLuggage,
    int ChildSeats,
    VehicleCategory VehicleCategory,
    decimal DistanceKm,
    int DurationMinutes,
    decimal TotalPrice,
    string? FlightNumber,
    Guid? AssignmentId,
    string? DriverName,
    string? VehiclePlate,
    bool IsReturnLeg,
    /// <summary>The partner or Booking that sent the booking; null for the website.</summary>
    string? PartnerName);

public sealed record BookingDetail(
    Guid Id,
    string Reference,
    BookingStatus Status,
    BookingSource Source,
    DateTime PickupAt,
    GeoPoint Pickup,
    GeoPoint Dropoff,
    MeetingPoint? PickupMeetingPoint,
    MeetingPoint? DropoffMeetingPoint,
    int Passengers,
    int LargeLuggage,
    int SmallLuggage,
    IReadOnlyList<ChildSeatRequest> ChildSeats,
    string? FlightNumber,
    bool NameSign,
    string? NameSignText,
    string? ClientNotes,
    string? InternalNotes,
    VehicleCategory VehicleCategory,
    decimal DistanceKm,
    int DurationMinutes,
    decimal TotalPrice,
    string Currency,
    IReadOnlyList<PriceLine> PriceLines,
    BookingClient Client,
    PaymentStatus? PaymentStatus,
    Guid? AssignmentId,
    string? DriverName,
    string? VehiclePlate,
    string? LinkedReference,
    string? CancellationReason,
    DateTimeOffset CreatedAt,
    string? PartnerName,
    string? ExternalReference);

public sealed record BookingClient(string Name, string? Email, string Phone, string Language);

public sealed record BookingStatusRequest
{
    public BookingStatus Status { get; init; }
    [MaxLength(300)]
    public string? Reason { get; init; }
}

public sealed record BookingNotesRequest
{
    [MaxLength(2000)]
    public string? InternalNotes { get; init; }
}
