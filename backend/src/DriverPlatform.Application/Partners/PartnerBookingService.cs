using System.Globalization;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Bookings;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Dispatch;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Notifications;
using DriverPlatform.Application.Quotes;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Partners;

public interface IPartnerBookingService
{
    Task<QuoteResponse> QuoteAsync(Guid partnerId, QuoteRequest request, CancellationToken cancellationToken);
    Task<PartnerBookingResult> CreateAsync(Guid partnerId, PartnerBookingRequest request, string? ip, CancellationToken cancellationToken);
    Task<PartnerBooking> GetAsync(Guid partnerId, string reference, CancellationToken cancellationToken);
    Task<PartnerBookingPage> ListAsync(Guid partnerId, DateTimeOffset? updatedSince, CancellationToken cancellationToken);
    Task<PartnerBooking> CancelAsync(Guid partnerId, string reference, PartnerCancelRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Business plan §6: partners get an API key and can check prices, create bookings and follow their
/// status. Partner bookings skip online payment (monthly invoicing) and go straight to the dispatcher.
/// </summary>
public sealed class PartnerBookingService(
    IAppDbContext db,
    IQuoteService quotes,
    INotificationService notifications,
    IAuditLogger audit,
    TimeProvider clock) : IPartnerBookingService
{
    private const int PageSize = 100;

    public async Task<QuoteResponse> QuoteAsync(Guid partnerId, QuoteRequest request, CancellationToken cancellationToken)
    {
        var partner = await PartnerAsync(partnerId, cancellationToken);
        return PartnerPricing.Adjust(await quotes.QuoteAsync(request, cancellationToken), partner.PriceAdjustmentPercent);
    }

    public async Task<PartnerBookingResult> CreateAsync(
        Guid partnerId, PartnerBookingRequest request, string? ip, CancellationToken cancellationToken)
    {
        var partner = await PartnerAsync(partnerId, cancellationToken);
        var externalReference = request.ExternalReference.Trim();

        // Idempotent: a retried request (timeout, network error) never creates a second ride.
        var existing = await Query(partnerId)
            .FirstOrDefaultAsync(b => b.ExternalReference == externalReference && b.ReturnOfBookingId == null, cancellationToken);
        if (existing is not null)
        {
            return new PartnerBookingResult(await ToDtoAsync(existing, cancellationToken), Created: false);
        }

        var quote = PartnerPricing.Adjust(await quotes.QuoteAsync(request.Trip, cancellationToken), partner.PriceAdjustmentPercent);
        var vehicle = quote.Vehicles.FirstOrDefault(v => v.Category == request.VehicleCategory)
            ?? throw new RequestValidationException("vehicleCategory", "This vehicle category is not offered.");
        if (!vehicle.Available)
        {
            throw new RequestValidationException("vehicleCategory", vehicle.UnavailableReason ?? "This vehicle does not fit the trip.");
        }
        if (request.ExpectedTotal is { } expected && expected != vehicle.Total)
        {
            throw new ConflictException("The price has changed. Check the new price and send the booking again.", quote);
        }

        var client = await ClientAsync(request.Passenger, cancellationToken);
        var source = partner.Kind == PartnerKind.Booking ? BookingSource.Booking : BookingSource.Partner;
        var trip = request.Trip;
        var extras = new BookingExtras(request.FlightNumber, trip.NameSign, request.NameSignText, request.Notes);

        var outbound = BookingFactory.Create(client, quote.Outbound, vehicle.Outbound, trip, request.VehicleCategory,
            extras, quote.VatRatePercent, source, BookingStatus.New, ip);
        outbound.Reference = await db.NewReferenceAsync(cancellationToken);
        outbound.PartnerId = partner.Id;
        outbound.ExternalReference = externalReference;
        db.Bookings.Add(outbound);

        if (quote.Return is not null && vehicle.Return is not null)
        {
            var inbound = BookingFactory.Create(client, quote.Return, vehicle.Return, trip, request.VehicleCategory,
                extras with { FlightNumber = trip.Return!.FlightNumber, NameSign = trip.Return.NameSign },
                quote.VatRatePercent, source, BookingStatus.New, ip);
            inbound.Reference = await db.NewReferenceAsync(cancellationToken);
            inbound.PartnerId = partner.Id;
            inbound.ExternalReference = externalReference;
            inbound.ReturnOfBookingId = outbound.Id;
            db.Bookings.Add(inbound);
        }

        audit.LogAs($"partner {partner.Name}", "partner.booking_created", nameof(Booking), outbound.Id.ToString(),
            new { outbound.Reference, externalReference, vehicle.Total });
        await db.SaveChangesAsync(cancellationToken);
        return new PartnerBookingResult(await ToDtoAsync(await FindAsync(partnerId, outbound.Reference, cancellationToken), cancellationToken), Created: true);
    }

    public async Task<PartnerBooking> GetAsync(Guid partnerId, string reference, CancellationToken cancellationToken) =>
        await ToDtoAsync(await FindAsync(partnerId, reference, cancellationToken), cancellationToken);

    /// <summary>For polling: bookings changed since a time, oldest first. Pass <c>NextCursor</c> back as <c>updatedSince</c>.</summary>
    public async Task<PartnerBookingPage> ListAsync(Guid partnerId, DateTimeOffset? updatedSince, CancellationToken cancellationToken)
    {
        var since = updatedSince ?? DateTimeOffset.MinValue;
        var bookings = await Query(partnerId)
            .Where(b => b.UpdatedAt > since)
            .OrderBy(b => b.UpdatedAt).ThenBy(b => b.Id)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        var items = new List<PartnerBooking>();
        foreach (var booking in bookings)
        {
            items.Add(await ToDtoAsync(booking, cancellationToken));
        }
        var cursor = bookings.Count == PageSize
            ? bookings[^1].UpdatedAt.ToString("O", CultureInfo.InvariantCulture)
            : null;
        return new PartnerBookingPage(items, cursor);
    }

    public async Task<PartnerBooking> CancelAsync(Guid partnerId, string reference, PartnerCancelRequest request, CancellationToken cancellationToken)
    {
        var booking = await FindAsync(partnerId, reference, cancellationToken);
        if (booking.Status == BookingStatus.Cancelled)
        {
            return await ToDtoAsync(booking, cancellationToken);
        }
        if (booking.Status is BookingStatus.PickedUp or BookingStatus.Completed)
        {
            throw new ConflictException("The ride has already started and can no longer be cancelled.");
        }

        var now = clock.GetUtcNow();
        var shift = booking.Assignment;
        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = now;
        booking.CancellationReason = $"Cancelled by partner: {request.Reason.Trim()}";
        if (shift is not null)
        {
            await ShiftService.SyncAsync(db, shift, now, cancellationToken);
        }

        audit.LogAs($"partner {booking.Partner?.Name ?? partnerId.ToString()}", "partner.booking_cancelled", nameof(Booking), booking.Id.ToString(), new { booking.Reference, request.Reason });
        await db.SaveChangesAsync(cancellationToken);

        // A driver who already has the shift learns about the change at once.
        if (shift is { SentAt: not null })
        {
            var loaded = await db.Assignments
                .Include(a => a.Driver).Include(a => a.Vehicle).Include(a => a.Bookings)
                .FirstAsync(a => a.Id == shift.Id, cancellationToken);
            await notifications.SendShiftToDriverAsync(loaded, NotificationKind.DriverShiftChanged, cancellationToken);
        }
        return await ToDtoAsync(booking, cancellationToken);
    }

    private async Task<Partner> PartnerAsync(Guid partnerId, CancellationToken cancellationToken) =>
        await db.Partners.AsNoTracking().FirstOrDefaultAsync(p => p.Id == partnerId && p.IsActive, cancellationToken)
        ?? throw new NotFoundException("Partner not found.");

    private IQueryable<Booking> Query(Guid partnerId) => db.Bookings
        .Include(b => b.Partner)
        .Include(b => b.Assignment).ThenInclude(a => a!.Driver)
        .Include(b => b.Assignment).ThenInclude(a => a!.Vehicle)
        .Where(b => b.PartnerId == partnerId);

    /// <summary>By our reference ("MD-…") or the partner's own id. Other partners' bookings are invisible.</summary>
    private async Task<Booking> FindAsync(Guid partnerId, string reference, CancellationToken cancellationToken)
    {
        var value = reference.Trim();
        return await Query(partnerId)
            .Where(b => b.Reference == value.ToUpperInvariant() || (b.ExternalReference == value && b.ReturnOfBookingId == null))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Booking not found.");
    }

    /// <summary>Same passenger (phone and email, or phone alone when there is no email) reuses the client record.</summary>
    private async Task<Client> ClientAsync(PassengerInput passenger, CancellationToken cancellationToken)
    {
        var phone = BookingFactory.NormalizePhone(passenger.Phone);
        var email = BookingFactory.Normalize(passenger.Email)?.ToLowerInvariant();
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Phone == phone && c.Email == email, cancellationToken);
        if (client is null)
        {
            client = new Client
            {
                FirstName = passenger.FirstName.Trim(),
                LastName = passenger.LastName.Trim(),
                Phone = phone,
                Email = email,
            };
            db.Clients.Add(client);
        }
        client.Language = passenger.Language;
        return client;
    }

    private async Task<PartnerBooking> ToDtoAsync(Booking b, CancellationToken cancellationToken)
    {
        var returnLeg = b.ReturnOfBookingId is null
            ? await db.Bookings.Where(x => x.ReturnOfBookingId == b.Id).Select(x => x.Reference).FirstOrDefaultAsync(cancellationToken)
            : null;
        var shift = b.Assignment;
        // The driver's details are shared only once the driver has accepted the shift.
        var driver = shift is { Status: AssignmentStatus.Confirmed or AssignmentStatus.Completed } && b.Status != BookingStatus.Cancelled
            ? new PartnerDriver(
                shift.Driver.FirstName,
                shift.Driver.Phone,
                string.Join(" ", new[] { shift.Vehicle.Color, shift.Vehicle.Make, shift.Vehicle.Model }.Where(s => !string.IsNullOrEmpty(s))),
                shift.Vehicle.PlateNumber)
            : null;

        return new PartnerBooking(
            b.Reference, b.ExternalReference ?? "", b.Status, ItalyTime.ToLocal(b.PickupAt), b.PickupAt,
            b.Pickup, b.Dropoff, b.PickupMeetingPoint, b.DropoffMeetingPoint, b.VehicleCategory, b.Passengers,
            b.DistanceKm, b.DurationMinutes, b.TotalPrice, b.Currency, b.VatRate, driver, returnLeg,
            b.CancellationReason, b.UpdatedAt);
    }
}
