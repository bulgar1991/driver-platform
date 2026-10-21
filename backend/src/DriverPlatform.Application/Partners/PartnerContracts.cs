using System.ComponentModel.DataAnnotations;
using DriverPlatform.Application.Quotes;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;

namespace DriverPlatform.Application.Partners;

public sealed record PassengerInput
{
    [Required, MaxLength(100)]
    public required string FirstName { get; init; }
    [Required, MaxLength(100)]
    public required string LastName { get; init; }
    /// <summary>With the country code. The driver calls this number.</summary>
    [Required, Phone, MaxLength(32)]
    public required string Phone { get; init; }
    [EmailAddress, MaxLength(254)]
    public string? Email { get; init; }
    [RegularExpression("^[a-z]{2}$")]
    public string Language { get; init; } = "en";
}

public sealed record PartnerBookingRequest
{
    /// <summary>Your id for this order. Sending the same id again returns the existing booking instead of a duplicate.</summary>
    [Required, MaxLength(64)]
    public required string ExternalReference { get; init; }
    [Required]
    public required QuoteRequest Trip { get; init; }
    public VehicleCategory VehicleCategory { get; init; }
    [Required]
    public required PassengerInput Passenger { get; init; }
    /// <summary>The price you showed; if ours differs the API answers 409 with the new quote.</summary>
    public decimal? ExpectedTotal { get; init; }
    [MaxLength(16), RegularExpression("^[A-Za-z0-9 ]{2,16}$")]
    public string? FlightNumber { get; init; }
    [MaxLength(60)]
    public string? NameSignText { get; init; }
    [MaxLength(2000)]
    public string? Notes { get; init; }
}

public sealed record PartnerCancelRequest
{
    [Required, MaxLength(300)]
    public required string Reason { get; init; }
}

public sealed record PartnerDriver(string FirstName, string Phone, string Vehicle, string PlateNumber);

public sealed record PartnerBooking(
    string Reference,
    string ExternalReference,
    BookingStatus Status,
    /// <summary>Italian local time.</summary>
    DateTime PickupAt,
    DateTimeOffset PickupAtUtc,
    GeoPoint Pickup,
    GeoPoint Dropoff,
    MeetingPoint? PickupMeetingPoint,
    MeetingPoint? DropoffMeetingPoint,
    VehicleCategory VehicleCategory,
    int Passengers,
    decimal DistanceKm,
    int DurationMinutes,
    decimal TotalPrice,
    string Currency,
    decimal VatRatePercent,
    /// <summary>Set once the driver has confirmed the shift.</summary>
    PartnerDriver? Driver,
    string? ReturnLegReference,
    string? CancellationReason,
    DateTimeOffset UpdatedAt);

public sealed record PartnerBookingResult(PartnerBooking Booking, bool Created);

public sealed record PartnerBookingPage(IReadOnlyList<PartnerBooking> Items, string? NextCursor);
