using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Pricing;
using DriverPlatform.Application.Quotes;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Bookings;

/// <summary>What the client chose beyond the trip itself; it reaches the driver unchanged.</summary>
public sealed record BookingExtras(string? FlightNumber, bool NameSign, string? NameSignText, string? Notes);

/// <summary>Builds bookings from a quote, the same way for the website and for partners.</summary>
public static class BookingFactory
{
    public static Booking Create(
        Client client,
        QuoteLeg leg,
        LegPrice price,
        QuoteRequest trip,
        VehicleCategory category,
        BookingExtras extras,
        decimal vatRate,
        BookingSource source,
        BookingStatus status,
        string? clientIp) => new()
    {
        Source = source,
        Status = status,
        Client = client,
        Pickup = ToGeoPoint(leg.Pickup),
        Dropoff = ToGeoPoint(leg.Dropoff),
        PickupMeetingPoint = leg.PickupMeetingPoint,
        DropoffMeetingPoint = leg.DropoffMeetingPoint,
        PickupAt = leg.PickupAtUtc,
        Passengers = trip.Passengers,
        LargeLuggage = trip.LargeLuggage,
        SmallLuggage = trip.SmallLuggage,
        ChildSeats = trip.ChildSeats.Select(s => new ChildSeatRequest { Group = s.Group, Count = s.Count }).ToList(),
        FlightNumber = Normalize(extras.FlightNumber)?.ToUpperInvariant(),
        NameSign = extras.NameSign,
        NameSignText = extras.NameSign
            ? Normalize(extras.NameSignText) ?? $"{client.FirstName} {client.LastName}".Trim()
            : null,
        ClientNotes = Normalize(extras.Notes),
        VehicleCategory = category,
        DistanceKm = leg.DistanceKm,
        DurationMinutes = leg.DurationMinutes,
        BaseFare = price.Fare,
        SurchargePercent = price.SurchargePercent,
        SurchargeTotal = price.SurchargeTotal,
        DiscountTotal = price.DiscountTotal,
        ZoneFeesTotal = price.ZoneFeesTotal,
        ExtrasTotal = price.ExtrasTotal,
        TotalPrice = price.Total,
        VatRate = vatRate,
        Currency = PricingDefaults.Currency,
        PriceLines = price.Lines.ToList(),
        ClientIp = clientIp,
    };

    public static async Task<string> NewReferenceAsync(this IAppDbContext db, CancellationToken cancellationToken)
    {
        while (true)
        {
            var reference = BookingReference.Create();
            var taken = await db.Bookings.AnyAsync(b => b.Reference == reference, cancellationToken)
                || db.Bookings.Local.Any(b => b.Reference == reference);
            if (!taken)
            {
                return reference;
            }
        }
    }

    public static string NormalizePhone(string phone) =>
        new(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());

    public static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static GeoPoint ToGeoPoint(PointInput p) =>
        new() { Address = p.Address.Trim(), Latitude = p.Latitude, Longitude = p.Longitude };
}
