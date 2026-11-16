using DriverPlatform.Application.BookingCom;

namespace DriverPlatform.Tests.Infrastructure;

/// <summary>Booking.com in memory: status moves as in the real API, and a stale state hash is refused with 400.</summary>
public sealed class FakeBookingComApi : IBookingComApi
{
    private readonly Dictionary<string, BcBooking> _bookings = [];
    private int _version;

    public List<(string Reference, string Response, string? Reason)> Responses { get; } = [];
    public List<(string Reference, BcDriverAssignment Assignment)> Drivers { get; } = [];
    public List<(string Reference, string Event, DateTimeOffset At)> Events { get; } = [];

    public BcBooking this[string bookingReference] => _bookings[bookingReference];

    /// <summary>Adds or changes a ride, as the customer would on Booking.com; a new state hash each time.</summary>
    public void Put(BcBooking booking) => _bookings[booking.BookingReference] = booking with { StateHash = $"h{++_version}" };

    public Task<IReadOnlyList<BcBooking>> ListAsync(DateTimeOffset pickupFrom, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BcBooking>>(_bookings.Values.Where(b => b.Status != BcStatus.Complete).ToList());

    public Task<BcBooking> GetAsync(string customerReference, string bookingReference, CancellationToken cancellationToken) =>
        Task.FromResult(Find(customerReference, bookingReference));

    public Task RespondAsync(string customerReference, string bookingReference, bool accept, string stateHash, string? rejectReason,
        CancellationToken cancellationToken)
    {
        var booking = Find(customerReference, bookingReference);
        CheckHash(booking, stateHash);
        var status = (booking.Status, accept) switch
        {
            (BcStatus.New or BcStatus.PendingAmendment, true) => BcStatus.Accepted,
            (BcStatus.New, false) => BcStatus.Declined,
            (BcStatus.PendingCancellation, true) => BcStatus.Cancelled,
            (BcStatus.Accepted or BcStatus.DriverAssigned or BcStatus.PendingAmendment, false) => BcStatus.Cancelled,
            _ => throw new BookingComApiException(400, $"Cannot respond to a {booking.Status} booking"),
        };
        Responses.Add((bookingReference, accept ? "ACCEPT" : "REJECT", rejectReason));
        Put(booking with { Status = status });
        return Task.CompletedTask;
    }

    public Task AssignDriverAsync(string bookingReference, BcDriverAssignment assignment, CancellationToken cancellationToken)
    {
        var booking = _bookings[bookingReference];
        CheckHash(booking, assignment.StateHash);
        if (booking.Status is not (BcStatus.Accepted or BcStatus.DriverAssigned))
        {
            throw new BookingComApiException(400, "Booking must be accepted");
        }
        Drivers.Add((bookingReference, assignment));
        Put(booking with { Status = BcStatus.DriverAssigned });
        return Task.CompletedTask;
    }

    public Task SendDriverEventAsync(string customerReference, string bookingReference, string eventType, DateTimeOffset occurredAt,
        double? latitude, double? longitude, CancellationToken cancellationToken)
    {
        Find(customerReference, bookingReference);
        if (Events.Count > 0 && occurredAt < Events[^1].At)
        {
            throw new BookingComApiException(400, "Event time is before last event time");
        }
        Events.Add((bookingReference, eventType, occurredAt));
        return Task.CompletedTask;
    }

    private BcBooking Find(string customerReference, string bookingReference) =>
        _bookings.TryGetValue(bookingReference, out var b) && b.CustomerReference == customerReference
            ? b
            : throw new BookingComApiException(404, "Booking Not Found");

    private static void CheckHash(BcBooking booking, string stateHash)
    {
        if (booking.StateHash != stateHash)
        {
            throw new BookingComApiException(400, "Invalid state_hash for booking status.");
        }
    }
}
