using System.Text.Json.Serialization;

namespace DriverPlatform.Application.BookingCom;

// The Booking.com Taxi Supplier API, as documented at https://connect.taxi.booking.com.
// Field names mix camelCase and snake_case, so every one is spelled out.

public sealed record BcBooking
{
    [JsonPropertyName("customerReference")] public string CustomerReference { get; init; } = "";
    [JsonPropertyName("bookingReference")] public string BookingReference { get; init; } = "";
    [JsonPropertyName("state_hash")] public string StateHash { get; init; } = "";
    [JsonPropertyName("status")] public string Status { get; init; } = "";
    [JsonPropertyName("booked_date")] public DateTime? BookedDate { get; init; }
    /// <summary>UTC, without an offset.</summary>
    [JsonPropertyName("pickup_date_time")] public DateTime PickupDateTime { get; init; }
    [JsonPropertyName("pickup_date_time_zone")] public string? PickupTimeZone { get; init; }
    [JsonPropertyName("flight_number")] public string? FlightNumber { get; init; }
    [JsonPropertyName("vehicle_type")] public string? VehicleType { get; init; }
    [JsonPropertyName("passenger_count")] public int PassengerCount { get; init; }
    [JsonPropertyName("meet_and_greet")] public bool MeetAndGreet { get; init; }
    [JsonPropertyName("additional_comments")] public string? AdditionalComments { get; init; }
    [JsonPropertyName("ancillaries")] public List<BcAncillary>? Ancillaries { get; init; }
    [JsonPropertyName("price")] public BcPrice? Price { get; init; }
    [JsonPropertyName("passenger")] public BcPassenger? Passenger { get; init; }
    [JsonPropertyName("pickup")] public BcLocation Pickup { get; init; } = new();
    [JsonPropertyName("dropoff")] public BcLocation Dropoff { get; init; } = new();
}

public sealed record BcAncillary
{
    /// <summary>BABY_SEAT, CHILD_SEAT or BOOSTER_SEAT.</summary>
    [JsonPropertyName("type")] public string Type { get; init; } = "";
    [JsonPropertyName("quantity")] public int Quantity { get; init; }
}

public sealed record BcPrice
{
    /// <summary>What Booking.com pays us, meet and greet included; "0" once cancelled.</summary>
    [JsonPropertyName("amount")] public string Amount { get; init; } = "0";
    [JsonPropertyName("currency")] public string Currency { get; init; } = "EUR";
}

public sealed record BcPassenger
{
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("telephone_number")] public string? TelephoneNumber { get; init; }
}

public sealed record BcLocation
{
    [JsonPropertyName("latitude")] public double Latitude { get; init; }
    [JsonPropertyName("longitude")] public double Longitude { get; init; }
    [JsonPropertyName("address")] public string? Address { get; init; }
    [JsonPropertyName("establishment_name")] public string? EstablishmentName { get; init; }
    [JsonPropertyName("postcode")] public string? Postcode { get; init; }
    [JsonPropertyName("country")] public string? Country { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
}

public sealed record BcDriverAssignment
{
    [JsonPropertyName("driver")] public required BcDriver Driver { get; init; }
    [JsonPropertyName("vehicle")] public required BcVehicle Vehicle { get; init; }
    [JsonPropertyName("stateHash")] public required string StateHash { get; init; }
}

public sealed record BcDriver
{
    [JsonPropertyName("firstName")] public required string FirstName { get; init; }
    [JsonPropertyName("lastName")] public required string LastName { get; init; }
    [JsonPropertyName("telephoneNumber")] public required string TelephoneNumber { get; init; }
}

public sealed record BcVehicle
{
    [JsonPropertyName("make")] public string? Make { get; init; }
    [JsonPropertyName("model")] public string? Model { get; init; }
    [JsonPropertyName("registration")] public string? Registration { get; init; }
    [JsonPropertyName("colour")] public string? Colour { get; init; }
}

public static class BcStatus
{
    public const string New = "NEW";
    public const string Accepted = "ACCEPTED";
    public const string DriverAssigned = "DRIVER_ASSIGNED";
    public const string PendingAmendment = "PENDING_AMENDMENT";
    public const string PendingCancellation = "PENDING_CANCELLATION";
    public const string Cancelled = "CANCELLED";
    public const string Declined = "DECLINED";
    public const string Complete = "COMPLETE";
}

public static class BcEvent
{
    public const string DepartedToPickup = "DRIVER_DEPARTED_TO_PICKUP";
    public const string ArrivedAtPickup = "DRIVER_ARRIVED_AT_PICKUP";
    public const string DepartedToDropoff = "DRIVER_DEPARTED_TO_DROPOFF";
    public const string ArrivedAtDropoff = "DRIVER_ARRIVED_AT_DROPOFF";
}

/// <summary>Booking.com answered with an error. <see cref="IsPermanent"/>: retrying the same call will not help (400, 404).</summary>
public sealed class BookingComApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public bool IsPermanent => StatusCode is 400 or 404 or 409 or 422;
}

/// <summary>The Booking.com Taxi Supplier API. Errors throw <see cref="BookingComApiException"/> or <see cref="HttpRequestException"/>.</summary>
public interface IBookingComApi
{
    /// <summary>Every ride with a pickup from <paramref name="pickupFrom"/> on, all pages. Completed rides are not listed.</summary>
    Task<IReadOnlyList<BcBooking>> ListAsync(DateTimeOffset pickupFrom, CancellationToken cancellationToken);
    Task<BcBooking> GetAsync(string customerReference, string bookingReference, CancellationToken cancellationToken);
    /// <param name="accept">ACCEPT, else REJECT with <paramref name="rejectReason"/>.</param>
    Task RespondAsync(string customerReference, string bookingReference, bool accept, string stateHash, string? rejectReason, CancellationToken cancellationToken);
    Task AssignDriverAsync(string bookingReference, BcDriverAssignment assignment, CancellationToken cancellationToken);
    Task SendDriverEventAsync(string customerReference, string bookingReference, string eventType, DateTimeOffset occurredAt,
        double? latitude, double? longitude, CancellationToken cancellationToken);
}
