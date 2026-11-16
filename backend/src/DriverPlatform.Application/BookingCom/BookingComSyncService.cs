using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Bookings;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Dispatch;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Notifications;
using DriverPlatform.Application.Pricing;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DriverPlatform.Application.BookingCom;

public sealed record BookingComSyncResult(
    int Fetched, int Imported, int Amended, int Cancelled, int Accepted, int Rejected, int DriversSent, int EventsSent, int Errors);

public interface IBookingComSyncService
{
    /// <summary>One round: fetch rides from Booking.com, apply them here, and send back answers, drivers and driver events.</summary>
    Task<BookingComSyncResult> SyncAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Booking.com rides become ordinary bookings (source "Booking", status "New") that the dispatcher
/// assigns as usual. Each round:
/// <list type="number">
/// <item>New rides are imported with Booking.com's price; amendments update the booking; a customer
/// cancellation cancels it (and tells a driver who already has the shift).</item>
/// <item>Rides are accepted (at once with <see cref="BookingComOptions.AutoAccept"/>, else once assigned);
/// a booking the staff cancel here is rejected there; a customer cancellation is acknowledged.</item>
/// <item>Once the driver confirms the shift, the driver and car are sent (again if either changes).</item>
/// <item>"Picked up" and "completed" from the driver link become Booking.com driver events.</item>
/// </list>
/// </summary>
public sealed class BookingComSyncService(
    IAppDbContext db,
    IBookingComApi api,
    IRoutingService routing,
    INotificationService notifications,
    IAuditLogger audit,
    IOptions<BookingComOptions> options,
    TimeProvider clock,
    ILogger<BookingComSyncService> logger) : IBookingComSyncService
{
    private const string Actor = "Booking.com";
    /// <summary>After an error a ride is left alone this long, unless Booking.com changes it.</summary>
    private static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(15);

    private static readonly HashSet<string> Importable = [BcStatus.New, BcStatus.Accepted, BcStatus.DriverAssigned, BcStatus.PendingAmendment];
    private static readonly HashSet<string> CancelledThere = [BcStatus.PendingCancellation, BcStatus.Cancelled, BcStatus.Declined];

    private int _imported, _amended, _cancelled, _accepted, _rejected, _driversSent, _eventsSent, _errors;

    public async Task<BookingComSyncResult> SyncAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var remote = await api.ListAsync(clock.GetUtcNow().AddHours(-settings.LookBackHours), cancellationToken);
        var references = remote.Select(r => r.BookingReference).ToList();
        var rides = await db.BookingComRides
            .Include(r => r.Booking).ThenInclude(b => b.Client)
            .Include(r => r.Booking).ThenInclude(b => b.Assignment).ThenInclude(a => a!.Driver)
            .Include(r => r.Booking).ThenInclude(b => b.Assignment).ThenInclude(a => a!.Vehicle)
            .Where(r => references.Contains(r.BookingReference))
            .ToDictionaryAsync(r => r.BookingReference, cancellationToken);

        foreach (var booking in remote.DistinctBy(r => r.BookingReference))
        {
            rides.TryGetValue(booking.BookingReference, out var ride);
            var previousHash = ride?.StateHash;
            var shiftsToTell = new HashSet<Guid>();
            try
            {
                ride = await ReceiveAsync(booking, ride, shiftsToTell, cancellationToken);
                if (ride is null)
                {
                    continue;
                }
                await db.SaveChangesAsync(cancellationToken);
                await TellDriversAsync(shiftsToTell, cancellationToken);

                if (ride.LastErrorAt is { } failedAt && clock.GetUtcNow() - failedAt < RetryAfter && previousHash == booking.StateHash)
                {
                    continue;
                }
                await RespondAsync(ride, settings, cancellationToken);
                await SendDriverAsync(ride, cancellationToken);
                await SendEventsAsync(ride, cancellationToken);
                ride.LastError = null;
                ride.LastErrorAt = null;
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is BookingComApiException or HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                _errors++;
                logger.LogWarning(ex, "Booking.com ride {Reference} could not be synchronised", booking.BookingReference);
                if (ride is not null)
                {
                    ride.LastError = Truncate(ex.Message, 500);
                    ride.LastErrorAt = clock.GetUtcNow();
                    await db.SaveChangesAsync(cancellationToken);
                }
            }
        }

        return new BookingComSyncResult(remote.Count, _imported, _amended, _cancelled, _accepted, _rejected, _driversSent, _eventsSent, _errors);
    }

    /// <summary>Applies what Booking.com says about the ride here. Null for a ride we never had and need not import.</summary>
    private async Task<BookingComRide?> ReceiveAsync(BcBooking remote, BookingComRide? ride, HashSet<Guid> shiftsToTell, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (ride is null)
        {
            return Importable.Contains(remote.Status) ? await ImportAsync(remote, cancellationToken) : null;
        }

        ride.Status = remote.Status;
        ride.StateHash = remote.StateHash;
        ride.LastSeenAt = now;
        var booking = ride.Booking;

        if (CancelledThere.Contains(remote.Status))
        {
            if (booking.Status is BookingStatus.Cancelled or BookingStatus.Completed)
            {
                return ride;
            }
            if (booking.Status == BookingStatus.PickedUp)
            {
                throw new BookingComApiException(409, "Booking.com cancelled a ride that is already under way; check it with the customer.");
            }
            ride.CancelledByBookingCom = true;
            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = now;
            booking.CancellationReason = "Cancelled on Booking.com";
            await SyncShiftAsync(booking, shiftsToTell, now, cancellationToken);
            audit.LogAs(Actor, "bookingcom.booking_cancelled", nameof(Booking), booking.Id.ToString(),
                new { booking.Reference, remote.BookingReference });
            _cancelled++;
            return ride;
        }

        var details = DetailsHash(remote);
        if (details != ride.DetailsHash && booking.Status is BookingStatus.New or BookingStatus.Assigned)
        {
            await ApplyAsync(remote, booking, cancellationToken);
            ride.DetailsHash = details;
            await SyncShiftAsync(booking, shiftsToTell, now, cancellationToken);
            audit.LogAs(Actor, "bookingcom.booking_amended", nameof(Booking), booking.Id.ToString(),
                new { booking.Reference, remote.BookingReference, booking.PickupAt });
            _amended++;
        }
        return ride;
    }

    private async Task<BookingComRide> ImportAsync(BcBooking remote, CancellationToken cancellationToken)
    {
        var partner = await PartnerAsync(cancellationToken);
        var (firstName, lastName) = SplitName(remote.Passenger?.Name);
        var phone = BookingFactory.NormalizePhone(remote.Passenger?.TelephoneNumber ?? "");
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Phone == phone && c.Email == null && phone != "", cancellationToken);
        if (client is null)
        {
            client = new Client { FirstName = firstName, LastName = lastName, Phone = phone };
            db.Clients.Add(client);
        }

        var booking = new Booking
        {
            Source = BookingSource.Booking,
            Status = BookingStatus.New,
            Client = client,
            PartnerId = partner.Id,
            ExternalReference = remote.BookingReference,
        };
        await ApplyAsync(remote, booking, cancellationToken);
        booking.Reference = await db.NewReferenceAsync(cancellationToken);
        db.Bookings.Add(booking);

        var ride = new BookingComRide
        {
            Booking = booking,
            CustomerReference = remote.CustomerReference,
            BookingReference = remote.BookingReference,
            Status = remote.Status,
            StateHash = remote.StateHash,
            DetailsHash = DetailsHash(remote),
            LastSeenAt = clock.GetUtcNow(),
            // Already accepted elsewhere (e.g. imported again after a database restore).
            AcceptedAt = remote.Status is BcStatus.Accepted or BcStatus.DriverAssigned ? clock.GetUtcNow() : null,
        };
        db.BookingComRides.Add(ride);
        audit.LogAs(Actor, "bookingcom.booking_imported", nameof(Booking), booking.Id.ToString(),
            new { booking.Reference, remote.CustomerReference, remote.BookingReference, booking.TotalPrice, booking.Currency });
        _imported++;
        return ride;
    }

    /// <summary>Accept, reject or acknowledge, depending on both sides' status.</summary>
    private async Task RespondAsync(BookingComRide ride, BookingComOptions settings, CancellationToken cancellationToken)
    {
        var booking = ride.Booking;
        var cancelledHere = booking.Status == BookingStatus.Cancelled && !ride.CancelledByBookingCom;
        bool? accept = ride.Status switch
        {
            BcStatus.New or BcStatus.PendingAmendment when cancelledHere => false,
            BcStatus.New or BcStatus.PendingAmendment when settings.AutoAccept || booking.Status != BookingStatus.New => true,
            BcStatus.Accepted or BcStatus.DriverAssigned when cancelledHere && ride.RejectedAt is null => false,
            // The customer cancelled: Booking.com waits for us to acknowledge it.
            BcStatus.PendingCancellation => true,
            _ => null,
        };
        if (accept is null)
        {
            return;
        }

        await api.RespondAsync(ride.CustomerReference, ride.BookingReference, accept.Value, ride.StateHash,
            accept.Value ? null : "OTHER", cancellationToken);
        var now = clock.GetUtcNow();
        if (ride.Status == BcStatus.PendingCancellation)
        {
            audit.LogAs(Actor, "bookingcom.cancellation_acknowledged", nameof(Booking), booking.Id.ToString(), new { booking.Reference });
        }
        else if (accept.Value)
        {
            ride.AcceptedAt ??= now;
            _accepted++;
            audit.LogAs(Actor, "bookingcom.booking_accepted", nameof(Booking), booking.Id.ToString(), new { booking.Reference, ride.Status });
        }
        else
        {
            ride.RejectedAt = now;
            _rejected++;
            audit.LogAs(Actor, "bookingcom.booking_rejected", nameof(Booking), booking.Id.ToString(),
                new { booking.Reference, booking.CancellationReason });
        }
        await RefreshAsync(ride, cancellationToken);
    }

    /// <summary>The driver and the car, once the driver has confirmed the shift; sent again if either changes.</summary>
    private async Task SendDriverAsync(BookingComRide ride, CancellationToken cancellationToken)
    {
        var booking = ride.Booking;
        var shift = booking.Assignment;
        if (ride.Status is not (BcStatus.Accepted or BcStatus.DriverAssigned)
            || booking.Status is BookingStatus.Cancelled
            || shift is not { Status: AssignmentStatus.Confirmed or AssignmentStatus.Completed })
        {
            return;
        }
        var key = $"{shift.DriverId}:{shift.VehicleId}";
        if (key == ride.DriverSentKey)
        {
            return;
        }

        var driver = shift.Driver;
        var vehicle = shift.Vehicle;
        await api.AssignDriverAsync(ride.BookingReference, new BcDriverAssignment
        {
            Driver = new BcDriver
            {
                FirstName = Truncate(driver.FirstName, 255),
                LastName = Truncate(driver.LastName, 255),
                TelephoneNumber = BookingFactory.NormalizePhone(driver.Phone),
            },
            Vehicle = new BcVehicle
            {
                Make = Truncate(vehicle.Make, 50),
                Model = Truncate(vehicle.Model, 50),
                Registration = Truncate(vehicle.PlateNumber, 50),
                Colour = vehicle.Color is null ? null : Truncate(vehicle.Color, 50),
            },
            StateHash = ride.StateHash,
        }, cancellationToken);

        ride.DriverSentKey = key;
        ride.DriverSentAt = clock.GetUtcNow();
        _driversSent++;
        audit.LogAs(Actor, "bookingcom.driver_sent", nameof(Booking), booking.Id.ToString(),
            new { booking.Reference, Driver = $"{driver.FirstName} {driver.LastName}", vehicle.PlateNumber });
        await RefreshAsync(ride, cancellationToken);
    }

    /// <summary>
    /// The driver link has "picked up" and "completed"; Booking.com wants four events in order, the
    /// first three of which happen around the pickup.
    /// </summary>
    private async Task SendEventsAsync(BookingComRide ride, CancellationToken cancellationToken)
    {
        var booking = ride.Booking;
        if (ride.DriverSentAt is null || ride.Status != BcStatus.DriverAssigned)
        {
            return;
        }

        var events = new List<(string Type, DateTimeOffset At, GeoPoint? Where)>();
        if (booking.PickedUpAt is { } pickedUp)
        {
            events.Add((BcEvent.DepartedToPickup, pickedUp, null));
            events.Add((BcEvent.ArrivedAtPickup, pickedUp, booking.Pickup));
            events.Add((BcEvent.DepartedToDropoff, pickedUp, booking.Pickup));
            if (booking.CompletedAt is { } completed)
            {
                events.Add((BcEvent.ArrivedAtDropoff, completed, booking.Dropoff));
            }
        }

        for (var i = ride.EventsSent; i < events.Count; i++)
        {
            var (type, at, where) = events[i];
            // Never before the driver was assigned or the previous event, and never in the future.
            var occurredAt = Max(Max(at, ride.DriverSentAt.Value.AddSeconds(1)), ride.LastEventAt ?? DateTimeOffset.MinValue);
            var now = clock.GetUtcNow();
            if (occurredAt > now)
            {
                occurredAt = now;
            }
            try
            {
                await api.SendDriverEventAsync(ride.CustomerReference, ride.BookingReference, type, occurredAt,
                    where?.Latitude, where?.Longitude, cancellationToken);
                _eventsSent++;
            }
            catch (BookingComApiException ex) when (ex.IsPermanent)
            {
                // A refused event is not sent again, so the following ones still go.
                logger.LogWarning("Booking.com refused {Event} for {Reference}: {Error}", type, ride.BookingReference, ex.Message);
                ride.LastError = Truncate($"{type}: {ex.Message}", 500);
            }
            ride.EventsSent = i + 1;
            ride.LastEventAt = occurredAt;
        }
    }

    private async Task RefreshAsync(BookingComRide ride, CancellationToken cancellationToken)
    {
        var fresh = await api.GetAsync(ride.CustomerReference, ride.BookingReference, cancellationToken);
        ride.Status = fresh.Status;
        ride.StateHash = fresh.StateHash;
    }

    /// <summary>The trip as Booking.com describes it. The price is Booking.com's and is never recalculated.</summary>
    private async Task ApplyAsync(BcBooking remote, Booking booking, CancellationToken cancellationToken)
    {
        var pickup = ToGeoPoint(remote.Pickup);
        var dropoff = ToGeoPoint(remote.Dropoff);
        var moved = booking.DistanceKm == 0
            || booking.Pickup.Latitude != pickup.Latitude || booking.Pickup.Longitude != pickup.Longitude
            || booking.Dropoff.Latitude != dropoff.Latitude || booking.Dropoff.Longitude != dropoff.Longitude;
        booking.Pickup = pickup;
        booking.Dropoff = dropoff;
        booking.PickupAt = new DateTimeOffset(DateTime.SpecifyKind(remote.PickupDateTime, DateTimeKind.Utc));
        booking.Passengers = Math.Max(1, remote.PassengerCount);
        booking.ChildSeats = (remote.Ancillaries ?? [])
            .Where(a => a.Quantity > 0 && ChildSeat(a.Type) is not null)
            .GroupBy(a => ChildSeat(a.Type)!.Value)
            .Select(g => new ChildSeatRequest { Group = g.Key, Count = g.Sum(a => a.Quantity) })
            .ToList();
        booking.FlightNumber = Clean(remote.FlightNumber, 16)?.ToUpperInvariant();
        var passengerName = Clean(remote.Passenger?.Name, 60);
        booking.NameSign = remote.MeetAndGreet;
        booking.NameSignText = remote.MeetAndGreet ? passengerName ?? $"{booking.Client.FirstName} {booking.Client.LastName}".Trim() : null;
        booking.ClientNotes = Clean(remote.AdditionalComments, 2000);
        booking.VehicleCategory = Category(remote.VehicleType);

        if (decimal.TryParse(remote.Price?.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount > 0)
        {
            booking.BaseFare = amount;
            booking.SurchargePercent = 0;
            booking.SurchargeTotal = 0;
            booking.DiscountTotal = 0;
            booking.ZoneFeesTotal = 0;
            booking.ExtrasTotal = 0;
            booking.TotalPrice = amount;
            booking.Currency = Clean(remote.Price!.Currency, 3)?.ToUpperInvariant() ?? PricingDefaults.Currency;
            booking.VatRate = (await db.LoadSettingsAsync(cancellationToken)).VatRatePercent;
            booking.PriceLines = [new PriceLine { Kind = PriceLineKind.Fare, Label = "Booking.com", Amount = amount }];
        }

        if (moved)
        {
            var route = await routing.GetRouteAsync(
                new Coordinate(pickup.Latitude, pickup.Longitude), new Coordinate(dropoff.Latitude, dropoff.Longitude), cancellationToken);
            booking.DistanceKm = route.DistanceKm;
            booking.DurationMinutes = route.DurationMinutes;
        }
    }

    private async Task SyncShiftAsync(Booking booking, HashSet<Guid> shiftsToTell, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (booking.Assignment is not { } shift)
        {
            return;
        }
        await ShiftService.SyncAsync(db, shift, now, cancellationToken);
        if (shift.SentAt is not null)
        {
            shiftsToTell.Add(shift.Id);
        }
    }

    /// <summary>A driver who already has the shift hears about the change at once.</summary>
    private async Task TellDriversAsync(HashSet<Guid> shiftIds, CancellationToken cancellationToken)
    {
        foreach (var id in shiftIds)
        {
            var shift = await db.Assignments
                .Include(a => a.Driver).Include(a => a.Vehicle).Include(a => a.Bookings)
                .FirstAsync(a => a.Id == id, cancellationToken);
            await notifications.SendShiftToDriverAsync(shift, NotificationKind.DriverShiftChanged, cancellationToken);
        }
    }

    /// <summary>The partner the rides are filed under (monthly statement); created with the first ride.</summary>
    private async Task<Partner> PartnerAsync(CancellationToken cancellationToken)
    {
        var partner = await db.Partners
            .Where(p => p.Kind == PartnerKind.Booking && p.IsActive)
            .OrderBy(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? db.Partners.Local.FirstOrDefault(p => p.Kind == PartnerKind.Booking && p.IsActive);
        if (partner is null)
        {
            partner = new Partner
            {
                Name = "Booking.com",
                Kind = PartnerKind.Booking,
                MonthlyInvoicing = true,
                // Booking.com talks to its own customers.
                NotifyPassengers = false,
            };
            db.Partners.Add(partner);
        }
        return partner;
    }

    private static GeoPoint ToGeoPoint(BcLocation location)
    {
        var address = Clean(location.Address, 300);
        var name = Clean(location.EstablishmentName, 120);
        var text = name is not null && (address is null || !address.Contains(name, StringComparison.OrdinalIgnoreCase))
            ? address is null ? name : $"{name}, {address}"
            : address ?? $"{location.Latitude:0.#####}, {location.Longitude:0.#####}";
        return new GeoPoint { Address = text, Latitude = location.Latitude, Longitude = location.Longitude };
    }

    /// <summary>Booking.com's car types on our four tariffs.</summary>
    public static VehicleCategory Category(string? vehicleType) => vehicleType?.ToUpperInvariant() switch
    {
        "EXECUTIVE" => VehicleCategory.Business,
        "LUXURY" or "ELECTRIC_LUXURY" => VehicleCategory.Estate,
        "PEOPLE_CARRIER" or "LARGE_PEOPLE_CARRIER" or "EXECUTIVE_PEOPLE_CARRIER" or "MINIBUS" => VehicleCategory.Van,
        _ => VehicleCategory.Sedan,
    };

    private static ChildSeatGroup? ChildSeat(string type) => type.ToUpperInvariant() switch
    {
        "BABY_SEAT" => ChildSeatGroup.Infant,
        "CHILD_SEAT" => ChildSeatGroup.Toddler,
        "BOOSTER_SEAT" => ChildSeatGroup.Booster,
        _ => null,
    };

    private static (string First, string Last) SplitName(string? name)
    {
        var parts = (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => ("Booking.com", "passenger"),
            1 => (Truncate(parts[0], 100), ""),
            _ => (Truncate(string.Join(' ', parts[..^1]), 100), Truncate(parts[^1], 100)),
        };
    }

    /// <summary>Everything about the trip, so a change (an amendment) is noticed; status and hash excluded.</summary>
    private static string DetailsHash(BcBooking b) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
    {
        b.PickupDateTime, b.FlightNumber, b.VehicleType, b.PassengerCount, b.MeetAndGreet, b.AdditionalComments,
        b.Ancillaries, b.Price, b.Passenger, b.Pickup, b.Dropoff,
    }))));

    private static string? Clean(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : Truncate(value.Trim(), max);

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
}
