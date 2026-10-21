using DriverPlatform.Domain.Common;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;

namespace DriverPlatform.Domain.Entities;

/// <summary>A point-to-point transfer. Everything the client chose here reaches the driver link unchanged.</summary>
public class Booking : Entity
{
    /// <summary>Human-readable code shown to clients, e.g. "MD-7K3QX9".</summary>
    public string Reference { get; set; } = "";
    public BookingSource Source { get; set; }
    public BookingStatus Status { get; set; }

    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public Guid? PartnerId { get; set; }
    public Partner? Partner { get; set; }
    /// <summary>The partner's or Booking's own id for this order.</summary>
    public string? ExternalReference { get; set; }

    public GeoPoint Pickup { get; set; } = new();
    public GeoPoint Dropoff { get; set; } = new();
    /// <summary>Set when the pickup address is inside a no-car zone; the route stops here.</summary>
    public MeetingPoint? PickupMeetingPoint { get; set; }
    public MeetingPoint? DropoffMeetingPoint { get; set; }
    public DateTimeOffset PickupAt { get; set; }

    public int Passengers { get; set; }
    public int LargeLuggage { get; set; }
    public int SmallLuggage { get; set; }
    public List<ChildSeatRequest> ChildSeats { get; set; } = [];
    public string? FlightNumber { get; set; }
    public bool NameSign { get; set; }
    public string? NameSignText { get; set; }
    public string? ClientNotes { get; set; }
    /// <summary>Dispatcher notes; never shown to the client.</summary>
    public string? InternalNotes { get; set; }
    /// <summary>"{driverId}:{vehicleId}" the client was last told about, so the message is sent once per driver/car.</summary>
    public string? DriverDetailsSentKey { get; set; }
    /// <summary>The outbound booking when this one is the return leg.</summary>
    public Guid? ReturnOfBookingId { get; set; }

    public VehicleCategory VehicleCategory { get; set; }
    public decimal DistanceKm { get; set; }
    public int DurationMinutes { get; set; }

    // Price is fixed at booking time and never recalculated after payment.
    public decimal BaseFare { get; set; }
    public decimal SurchargePercent { get; set; }
    public decimal SurchargeTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal ZoneFeesTotal { get; set; }
    public decimal ExtrasTotal { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal VatRate { get; set; }
    public string Currency { get; set; } = "EUR";
    /// <summary>The price as shown to the client, line by line; the invoice is built from it.</summary>
    public List<PriceLine> PriceLines { get; set; } = [];

    /// <summary>Kept for audit, fraud prevention and payment disputes.</summary>
    public string? ClientIp { get; set; }

    public Guid? AssignmentId { get; set; }
    public Assignment? Assignment { get; set; }
    public DateTimeOffset? PickedUpAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<Invoice> Invoices { get; set; } = [];
}
