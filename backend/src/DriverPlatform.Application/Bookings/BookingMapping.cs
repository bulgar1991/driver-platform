using System.Globalization;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Pricing;
using DriverPlatform.Domain.Entities;

namespace DriverPlatform.Application.Bookings;

public static class BookingMapping
{
    public static LegPrice ToLegPrice(this Booking b) => new(
        b.BaseFare, b.SurchargePercent, b.SurchargeTotal, b.DiscountTotal, b.ZoneFeesTotal, b.ExtrasTotal,
        b.TotalPrice, b.PriceLines);

    public static BookingLegSummary ToLegSummary(this Booking b) => new(
        b.Reference, ItalyTime.ToLocal(b.PickupAt), b.Pickup.Address, b.Dropoff.Address,
        b.PickupMeetingPoint, b.DropoffMeetingPoint, b.DistanceKm, b.DurationMinutes, b.ToLegPrice());

    /// <summary>One-line description for invoices, payment pages and emails.</summary>
    public static string Describe(this Booking b)
    {
        var from = b.PickupMeetingPoint?.Name ?? b.Pickup.Address;
        var to = b.DropoffMeetingPoint?.Name ?? b.Dropoff.Address;
        return string.Create(CultureInfo.InvariantCulture,
            $"Transfer {b.Reference}, {ItalyTime.ToLocal(b.PickupAt):dd/MM/yyyy HH:mm}: {from} → {to}");
    }
}
