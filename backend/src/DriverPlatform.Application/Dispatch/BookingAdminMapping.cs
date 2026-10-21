using DriverPlatform.Application.Geo;
using DriverPlatform.Domain.Entities;

namespace DriverPlatform.Application.Dispatch;

internal static class BookingAdminMapping
{
    /// <summary>Needs Client and Assignment (with Driver and Vehicle) loaded; Partner when there is one.</summary>
    public static BookingListItem ToListItem(this Booking b) => new(
        b.Id,
        b.Reference,
        b.Status,
        b.Source,
        ItalyTime.ToLocal(b.PickupAt),
        b.PickupMeetingPoint?.Name ?? b.Pickup.Address,
        b.DropoffMeetingPoint?.Name ?? b.Dropoff.Address,
        $"{b.Client.FirstName} {b.Client.LastName}".Trim(),
        b.Client.Phone,
        b.Passengers,
        b.LargeLuggage,
        b.SmallLuggage,
        b.ChildSeats.Sum(s => s.Count),
        b.VehicleCategory,
        b.DistanceKm,
        b.DurationMinutes,
        b.TotalPrice,
        b.FlightNumber,
        b.AssignmentId,
        b.Assignment is null ? null : $"{b.Assignment.Driver.FirstName} {b.Assignment.Driver.LastName}",
        b.Assignment?.Vehicle.PlateNumber,
        b.ReturnOfBookingId is not null,
        b.Partner?.Name);
}
