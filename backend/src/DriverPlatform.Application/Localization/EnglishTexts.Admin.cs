using System.Globalization;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Localization;

internal sealed partial class EnglishTexts
{
    public override string BookingStatusName(BookingStatus status) => status switch
    {
        BookingStatus.AwaitingPayment => "awaiting payment",
        BookingStatus.New => "to assign",
        BookingStatus.Assigned => "assigned",
        BookingStatus.PickedUp => "picked up",
        BookingStatus.Completed => "completed",
        _ => "cancelled",
    };
    public override string ShiftStatusName(AssignmentStatus status) => status switch
    {
        AssignmentStatus.Draft => "draft",
        AssignmentStatus.Sent => "sent to driver",
        AssignmentStatus.Seen => "seen by driver",
        AssignmentStatus.Confirmed => "confirmed",
        AssignmentStatus.Completed => "completed",
        _ => "cancelled",
    };
    public override string BookingStatusChange(BookingStatus from, BookingStatus to) =>
        $"A booking that is {BookingStatusName(from)} cannot become {BookingStatusName(to)}.";
    public override string ShiftStatusChange(AssignmentStatus from, AssignmentStatus to) =>
        $"A shift that is {ShiftStatusName(from)} cannot become {ShiftStatusName(to)}.";
    public override string ShiftLocked(AssignmentStatus status) => $"A shift that is {ShiftStatusName(status)} cannot be changed.";
    public override string SayWhyBookingCancelled => "Say why the booking is cancelled.";
    public override string SayWhyShiftCancelled => "Say why the shift is cancelled.";
    public override string AlreadyPickedUp => "The driver has already picked up a client on this shift.";
    public override string ChooseActiveDriver => "Choose an active driver.";
    public override string ChooseActiveVehicle => "Choose an active vehicle.";
    public override string ChooseBookings => "Choose one or more existing bookings.";
    public override string StartsBeforeEnds(string later, string earlier) => $"{later} starts before {earlier} ends.";
    public override string TightGap(int minutes, string earlier, string later) =>
        $"Only {minutes} min between {earlier} and {later}, plus the drive to the next pickup.";
    public override string MarkedNotAvailable => "Marked as not available";
    public override string OnAnotherShift => "Already on another shift at this time";

    public override string DocumentName(DriverDocumentType type) => type switch
    {
        DriverDocumentType.DrivingLicence => "Driving licence",
        DriverDocumentType.NccLicence => "NCC licence",
        DriverDocumentType.TaxiLicence => "Taxi licence",
        DriverDocumentType.ProfessionalCertificate => "Professional certificate (CAP/KB)",
        DriverDocumentType.Insurance => "Insurance",
        _ => "Criminal record",
    };
    public override string VehicleInsurance => "Insurance";
    public override string VehicleInspection => "Inspection (revisione)";
    public override string DocumentExpired(string document, DateOnly date) =>
        $"{document} expired on {date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";
    public override string NoDrivingLicence => "No driving licence on file";
    public override string NoNccOrTaxiLicence => "No NCC or taxi licence on file";
    public override string NoInsuranceDate => "No insurance date on file";
    public override string NoInspectionDate => "No inspection date on file";

    public override string DriverNotFound => "Driver not found.";
    public override string PhoneTaken => "Another driver already has this phone number.";
    public override string VehicleNotFound => "Vehicle not found.";
    public override string PlateTaken => "Another vehicle already has this plate.";
    public override string VehicleHasShifts => "This vehicle has been on shifts, so it can't be deleted. Untick Active instead.";
    public override string DriverHasShifts => "This driver has shifts or reviews, so they can't be deleted. Untick Active instead.";
    public override string PhotoTooLarge => "The picture is larger than 5 MB.";
    public override string PhotoNotAnImage => "Choose a JPEG, PNG or WebP picture.";
    public override string ZoneNotFound => "Zone not found.";
    public override string PickCentre => "Pick the centre on the map and set a radius.";
    public override string PolygonOutsideServiceArea => "All points must be in the service area (Italy, France, Germany, Switzerland).";
    public override string MeetingPointNameAndAddress => "Name and address are required.";
    public override string PlaceMeetingPoint => "Pick the meeting point on the map.";
    public override string MeetingPointNamesUnique => "Meeting point names must be unique within the zone.";
    public override string SurchargeNotFound => "Surcharge not found.";
    public override string PickADay => "Pick at least one day.";
    public override string EnterBothDates => "Enter both dates.";
    public override string EndBeforeStart => "The end date is before the start date.";
    public override string EnterBothTimes => "Enter both times.";
    public override string SameStartAndEnd => "Start and end time are the same.";
    public override string PartnerNotFound => "Partner not found.";
    public override string HttpsOnly => "Use an https:// address.";
    public override string PaymentNotFound => "Payment not found.";
    public override string OnlyPaidRefundable => "Only paid payments can be refunded.";
    public override string RefundRange(string max) => $"Refund between {Money(0.01m)} and {max}.";
    public override string ApplicationNotFound => "Application not found.";

    public override string EmailTaken => "There is already an account with this email.";
    public override string LastAdmin => "This is the last active admin. Make someone else admin first.";
    public override string CannotDeactivateSelf => "You cannot deactivate your own account.";
    public override string UserNotFound => "User not found.";
    public override string PasswordTooShort(int length) => $"Use at least {length} characters; a short sentence is easy to remember.";
    public override string PasswordTooEasy => "This password is too easy to guess.";
    public override string CurrentPasswordWrong => "The current password is not correct.";
    public override string ChooseDifferentPassword => "Choose a different password.";
}
