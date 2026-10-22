using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Localization;

/// <summary>What the staff read from the backend in the admin panel: errors, warnings, document names.</summary>
public abstract partial class Texts
{
    public abstract string BookingStatusName(BookingStatus status);
    public abstract string ShiftStatusName(AssignmentStatus status);
    public abstract string BookingStatusChange(BookingStatus from, BookingStatus to);
    public abstract string ShiftStatusChange(AssignmentStatus from, AssignmentStatus to);
    public abstract string ShiftLocked(AssignmentStatus status);
    public abstract string SayWhyBookingCancelled { get; }
    public abstract string SayWhyShiftCancelled { get; }
    public abstract string AlreadyPickedUp { get; }
    public abstract string ChooseActiveDriver { get; }
    public abstract string ChooseActiveVehicle { get; }
    public abstract string ChooseBookings { get; }
    public abstract string StartsBeforeEnds(string later, string earlier);
    public abstract string TightGap(int minutes, string earlier, string later);
    public abstract string MarkedNotAvailable { get; }
    public abstract string OnAnotherShift { get; }

    public abstract string DocumentName(DriverDocumentType type);
    public abstract string VehicleInsurance { get; }
    public abstract string VehicleInspection { get; }
    public abstract string DocumentExpired(string document, DateOnly date);
    public abstract string NoDrivingLicence { get; }
    public abstract string NoNccOrTaxiLicence { get; }
    public abstract string NoInsuranceDate { get; }
    public abstract string NoInspectionDate { get; }

    public abstract string DriverNotFound { get; }
    public abstract string PhoneTaken { get; }
    public abstract string VehicleNotFound { get; }
    public abstract string PlateTaken { get; }
    public abstract string ZoneNotFound { get; }
    public abstract string PickCentre { get; }
    public abstract string PolygonOutsideServiceArea { get; }
    public abstract string MeetingPointNameAndAddress { get; }
    public abstract string PlaceMeetingPoint { get; }
    public abstract string MeetingPointNamesUnique { get; }
    public abstract string SurchargeNotFound { get; }
    public abstract string PickADay { get; }
    public abstract string EnterBothDates { get; }
    public abstract string EndBeforeStart { get; }
    public abstract string EnterBothTimes { get; }
    public abstract string SameStartAndEnd { get; }
    public abstract string PartnerNotFound { get; }
    public abstract string HttpsOnly { get; }
    public abstract string PaymentNotFound { get; }
    public abstract string OnlyPaidRefundable { get; }
    public abstract string RefundRange(string max);
    public abstract string ApplicationNotFound { get; }

    public abstract string EmailTaken { get; }
    public abstract string LastAdmin { get; }
    public abstract string CannotDeactivateSelf { get; }
    public abstract string UserNotFound { get; }
    public abstract string PasswordTooShort(int length);
    public abstract string PasswordTooEasy { get; }
    public abstract string CurrentPasswordWrong { get; }
    public abstract string ChooseDifferentPassword { get; }
}
