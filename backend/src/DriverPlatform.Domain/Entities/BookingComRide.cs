using DriverPlatform.Domain.Common;

namespace DriverPlatform.Domain.Entities;

/// <summary>
/// A ride received from the Booking.com Taxi Supplier API, linked to our booking. Keeps what Booking.com
/// last told us (status, state hash) and what we already sent back (answer, driver, driver events).
/// </summary>
public class BookingComRide : Entity
{
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    /// <summary>The customer's order; a return trip has two rides with the same one.</summary>
    public string CustomerReference { get; set; } = "";
    /// <summary>This ride (leg). Unique.</summary>
    public string BookingReference { get; set; } = "";

    /// <summary>Booking.com's status: NEW, ACCEPTED, DRIVER_ASSIGNED, PENDING_AMENDMENT, PENDING_CANCELLATION, CANCELLED, DECLINED, COMPLETE.</summary>
    public string Status { get; set; } = "";
    /// <summary>Changes whenever Booking.com changes the ride; required to accept, reject or assign a driver.</summary>
    public string StateHash { get; set; } = "";
    /// <summary>The ride's details as last applied, to notice amendments.</summary>
    public string DetailsHash { get; set; } = "";
    public DateTimeOffset LastSeenAt { get; set; }

    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? RejectedAt { get; set; }
    /// <summary>Booking.com (the customer) cancelled; nothing is rejected back.</summary>
    public bool CancelledByBookingCom { get; set; }

    /// <summary>"{driverId}:{vehicleId}" last sent with assignDriver, so a change of driver or car is sent again.</summary>
    public string? DriverSentKey { get; set; }
    public DateTimeOffset? DriverSentAt { get; set; }
    /// <summary>Driver events sent so far, in order: departed to pickup, arrived at pickup, departed to drop-off, arrived at drop-off.</summary>
    public int EventsSent { get; set; }
    public DateTimeOffset? LastEventAt { get; set; }

    public string? LastError { get; set; }
    public DateTimeOffset? LastErrorAt { get; set; }
}
