using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Payments;
using DriverPlatform.Application.Pricing;
using DriverPlatform.Application.Quotes;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DriverPlatform.Application.Bookings;

/// <summary>Website bookings. The price is always recalculated here; the client's figure is only compared.</summary>
public sealed class BookingService(
    IAppDbContext db,
    IQuoteService quotes,
    IPaymentService payments,
    IBookingLinks links,
    TimeProvider clock,
    ILogger<BookingService> logger) : IBookingService
{
    public async Task<CreateBookingResponse> CreateAsync(
        CreateBookingRequest request, string? clientIp, CancellationToken cancellationToken)
    {
        if (!request.GdprConsent)
        {
            throw new RequestValidationException("gdprConsent", "Please accept the privacy policy to book.");
        }

        var quote = await quotes.QuoteAsync(request.Trip, cancellationToken);
        var vehicle = quote.Vehicles.FirstOrDefault(v => v.Category == request.VehicleCategory)
            ?? throw new RequestValidationException("vehicleCategory", "This vehicle category is not offered.");
        if (!vehicle.Available)
        {
            throw new RequestValidationException("vehicleCategory", vehicle.UnavailableReason ?? "This vehicle does not fit the trip.");
        }
        if (request.ExpectedTotal is { } expected && expected != vehicle.Total)
        {
            throw new ConflictException("The price has changed. Please check the new price and confirm again.", quote);
        }

        var client = await FindOrCreateClientAsync(request.Contact, request.MarketingConsent, cancellationToken);
        var trip = request.Trip;

        var outbound = NewBooking(client, quote.Outbound, vehicle.Outbound, trip, request, clientIp,
            trip.NameSign, request.FlightNumber, quote.VatRatePercent);
        outbound.Reference = await db.NewReferenceAsync(cancellationToken);
        db.Bookings.Add(outbound);

        var legs = new List<BookingLegSummary> { Summary(outbound, quote.Outbound, vehicle.Outbound) };
        if (quote.Return is not null && vehicle.Return is not null)
        {
            var inbound = NewBooking(client, quote.Return, vehicle.Return, trip, request, clientIp,
                trip.Return!.NameSign, trip.Return.FlightNumber, quote.VatRatePercent);
            inbound.Reference = await db.NewReferenceAsync(cancellationToken);
            inbound.ReturnOfBookingId = outbound.Id;
            db.Bookings.Add(inbound);
            legs.Add(Summary(inbound, quote.Return, vehicle.Return));
        }

        await db.SaveChangesAsync(cancellationToken);

        string? paymentUrl = null;
        try
        {
            paymentUrl = await payments.StartCheckoutAsync(outbound, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The booking is kept; the client pays later from the booking link.
            logger.LogError(ex, "Could not open the payment page for {Reference}", outbound.Reference);
        }

        return new CreateBookingResponse(
            outbound.Reference,
            outbound.Status,
            vehicle.Category,
            legs,
            vehicle.Total,
            vehicle.VatIncluded,
            quote.VatRatePercent,
            quote.Currency,
            paymentUrl,
            links.ManageUrl(outbound));
    }

    private static BookingLegSummary Summary(Booking booking, QuoteLeg leg, LegPrice price) => new(
        booking.Reference, leg.PickupAt, booking.Pickup.Address, booking.Dropoff.Address,
        booking.PickupMeetingPoint, booking.DropoffMeetingPoint, booking.DistanceKm, booking.DurationMinutes, price);

    private static Booking NewBooking(
        Client client,
        QuoteLeg leg,
        LegPrice price,
        QuoteRequest trip,
        CreateBookingRequest request,
        string? clientIp,
        bool nameSign,
        string? flightNumber,
        decimal vatRate) =>
        BookingFactory.Create(
            client, leg, price, trip, request.VehicleCategory,
            new BookingExtras(flightNumber, nameSign, request.NameSignText, request.Notes),
            vatRate, BookingSource.Website, BookingStatus.AwaitingPayment, clientIp);

    /// <summary>Reuses the client only when both email and phone match, so nobody can overwrite someone else's details.</summary>
    private async Task<Client> FindOrCreateClientAsync(ContactInput contact, bool marketingConsent, CancellationToken cancellationToken)
    {
        var email = contact.Email.Trim().ToLowerInvariant();
        var phone = BookingFactory.NormalizePhone(contact.Phone);
        var now = clock.GetUtcNow();

        var client = await db.Clients.FirstOrDefaultAsync(c => c.Email == email && c.Phone == phone, cancellationToken);
        if (client is null)
        {
            client = new Client
            {
                FirstName = contact.FirstName.Trim(),
                LastName = contact.LastName.Trim(),
                Email = email,
                Phone = phone,
            };
            db.Clients.Add(client);
        }
        client.Language = contact.Language;
        client.GdprConsentAt = now;
        client.MarketingConsent = marketingConsent;
        return client;
    }
}
