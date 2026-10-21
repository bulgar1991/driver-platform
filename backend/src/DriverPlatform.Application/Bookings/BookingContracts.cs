using System.ComponentModel.DataAnnotations;
using DriverPlatform.Application.Pricing;
using DriverPlatform.Application.Quotes;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;

namespace DriverPlatform.Application.Bookings;

public sealed record ContactInput
{
    [Required, MaxLength(100)]
    public required string FirstName { get; init; }
    [Required, MaxLength(100)]
    public required string LastName { get; init; }
    /// <summary>Required: the confirmation and the invoice are sent by email.</summary>
    [Required, EmailAddress, MaxLength(254)]
    public required string Email { get; init; }
    [Required, Phone, MaxLength(32)]
    public required string Phone { get; init; }
    [RegularExpression("^[a-z]{2}$")]
    public string Language { get; init; } = "en";
}

public sealed record CreateBookingRequest
{
    [Required]
    public required QuoteRequest Trip { get; init; }
    public VehicleCategory VehicleCategory { get; init; }
    /// <summary>The total the client saw. If the price changed since, the API answers 409 with the new quote.</summary>
    public decimal? ExpectedTotal { get; init; }
    [Required]
    public required ContactInput Contact { get; init; }
    [MaxLength(16), RegularExpression("^[A-Za-z0-9 ]{2,16}$")]
    public string? FlightNumber { get; init; }
    [MaxLength(60)]
    public string? NameSignText { get; init; }
    [MaxLength(2000)]
    public string? Notes { get; init; }
    /// <summary>Must be true: consent to processing personal data for the ride (GDPR).</summary>
    public bool GdprConsent { get; init; }
    public bool MarketingConsent { get; init; }
}

public sealed record BookingLegSummary(
    string Reference,
    DateTime PickupAt,
    string PickupAddress,
    string DropoffAddress,
    MeetingPoint? PickupMeetingPoint,
    MeetingPoint? DropoffMeetingPoint,
    decimal DistanceKm,
    int DurationMinutes,
    LegPrice Price);

public sealed record CreateBookingResponse(
    string Reference,
    BookingStatus Status,
    VehicleCategory VehicleCategory,
    IReadOnlyList<BookingLegSummary> Legs,
    decimal Total,
    decimal VatIncluded,
    decimal VatRatePercent,
    string Currency,
    /// <summary>Stripe's payment page; null if it could not be opened (the client can retry from <see cref="ManageUrl"/>).</summary>
    string? PaymentUrl,
    string ManageUrl);
