namespace DriverPlatform.Domain.Enums;

public enum StaffRole
{
    Admin,
    Dispatcher,
}

public enum VehicleCategory
{
    /// <summary>Berlină, 1–3 passengers.</summary>
    Sedan,
    /// <summary>Break / SUV, 1–4 passengers.</summary>
    Estate,
    /// <summary>Van, 5–8 passengers.</summary>
    Van,
}

public enum BookingSource
{
    Website,
    Booking,
    Partner,
}

public enum BookingStatus
{
    /// <summary>Created on the website, waiting for payment.</summary>
    AwaitingPayment,
    /// <summary>Paid or received from a partner; waiting for the dispatcher.</summary>
    New,
    Assigned,
    PickedUp,
    Completed,
    Cancelled,
}

public enum PaymentMethod
{
    Card,
    PayPal,
}

public enum PaymentStatus
{
    Pending,
    Succeeded,
    Failed,
    Refunded,
    PartiallyRefunded,
}

public enum InvoiceType
{
    Invoice,
    CreditNote,
}

public enum InvoiceStatus
{
    Issued,
    Sent,
    Cancelled,
}

public enum AssignmentStatus
{
    Draft,
    Sent,
    Seen,
    Confirmed,
    Completed,
    Cancelled,
}

public enum DriverDocumentType
{
    DrivingLicence,
    NccLicence,
    TaxiLicence,
    ProfessionalCertificate,
    Insurance,
    CriminalRecord,
}

public enum ZoneType
{
    /// <summary>No car access (e.g. historic centre of Venice); pickup moves to a meeting point.</summary>
    NoCarAccess,
    /// <summary>Zona a traffico limitato: fees or restrictions apply.</summary>
    LimitedTraffic,
    Airport,
    TrainStation,
    Port,
}

public enum ZoneShape
{
    Circle,
    Polygon,
}

public enum SurchargeKind
{
    DaysOfWeek,
    DateRange,
    TimeOfDay,
}

public enum ExtraType
{
    ChildSeat,
    ExtraLuggage,
    NameSign,
}

public enum ChildSeatGroup
{
    /// <summary>Group 0+, 0–13 kg.</summary>
    Infant,
    /// <summary>Group 1, 9–18 kg.</summary>
    Toddler,
    /// <summary>Group 2/3, 15–36 kg.</summary>
    Booster,
}

public enum NotificationChannel
{
    Email,
    WhatsApp,
    Sms,
}

public enum PriceLineKind
{
    /// <summary>Base fee + km × rate.</summary>
    Fare,
    Surcharge,
    /// <summary>Return-trip discount (negative amount).</summary>
    Discount,
    ZoneFee,
    Extra,
}

public enum NotificationKind
{
    /// <summary>Driver: a new shift with the personal link.</summary>
    DriverShift,
    /// <summary>Driver: an assigned shift was changed or cancelled.</summary>
    DriverShiftChanged,
    /// <summary>Client: the driver's name, phone and car.</summary>
    ClientDriverDetails,
    /// <summary>Client: the one-time review link after drop-off.</summary>
    ReviewRequest,
}

public enum NotificationStatus
{
    Sent,
    Failed,
    /// <summary>No address for this channel (e.g. a driver without email).</summary>
    Skipped,
}

public enum PartnerKind
{
    /// <summary>Agencies, hotels, cruise companies… Bookings get source "Partner".</summary>
    Company,
    /// <summary>Booking.com: bookings get source "Booking".</summary>
    Booking,
}
