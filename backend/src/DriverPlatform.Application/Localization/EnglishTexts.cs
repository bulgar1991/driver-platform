using System.Globalization;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Localization;

internal sealed partial class EnglishTexts : Texts
{
    public override string Language => "en";
    // Irish English: euros and day-first dates.
    public override CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("en-IE");

    public override string Category(VehicleCategory category) => category switch
    {
        VehicleCategory.Sedan => "Sedan",
        VehicleCategory.Estate => "Estate / SUV",
        _ => "Van",
    };
    public override string ReturnDiscount(decimal percent) => $"Return trip −{percent.ToString("0.##", Culture)}%";
    public override string ZoneAccess(string zone) => $"{zone} access";
    public override string ChildSeat => "Child seat";
    public override string ExtraLuggage => "Extra luggage";
    public override string NameSign => "Name sign";
    public override string PublicHolidays => "Public holidays";
    public override string UpToPassengers(int count) => $"Up to {count} passengers";
    public override string UpToLargeBags(int count) => $"Up to {count} large bags";

    public override string AcceptPrivacyToBook => "Please accept the privacy policy to book.";
    public override string AcceptPrivacy => "Please accept the privacy policy.";
    public override string CategoryNotOffered => "This vehicle category is not offered.";
    public override string VehicleDoesNotFit => "This vehicle does not fit the trip.";
    public override string PriceChanged => "The price has changed. Please check the new price and confirm again.";
    public override string BookingWasCancelled => "This booking was cancelled.";
    public override string BookingAlreadyPaid => "This booking is already paid.";
    public override string PayTheOutbound => "Pay the outbound booking; the return trip is included.";
    public override string ReturnAfterArrival => "The return pickup must be after the first ride arrives.";
    public override string ChildSeatLimit => "At most one child seat per passenger, and three per car.";
    public override string ChildSeatGroupOnce => "List each child seat group once.";
    public override string OutsideServiceArea => "We operate in Italy, France, Germany and Switzerland.";
    public override string TooClose => "Pickup and drop-off are too close to each other.";
    public override string TimeDoesNotExist => "This time does not exist locally (clocks go forward). Pick another time.";
    public override string BookAhead(int hours) => $"Book at least {hours} hours ahead.";
    public override string OneYearAhead => "Bookings open up to one year ahead.";
    public override string NoMeetingPointYet(string zone) => $"{zone} has no meeting point configured yet. Please contact us.";
    public override string NotAMeetingPoint(string requested, string zone) => $"\"{requested}\" is not a meeting point of {zone}.";
    public override string BookingNotFound => "Booking not found.";
    public override string InvoiceNotFound => "Invoice not found.";
    public override string AlreadyRated => "Thank you, this ride has already been rated.";
    public override string ReviewLinkInvalid => "This review link is not valid or has expired.";

    public override string ShiftNotFound => "Shift not found.";
    public override string ShiftAlreadyConfirmed => "Already confirmed.";
    public override string ShiftCannotBeConfirmed => "This shift can no longer be confirmed.";
    public override string RideNotFound => "Ride not found.";
    public override string ConfirmShiftFirst => "Confirm the shift first.";
    public override string NotWaitingForPickup => "This ride is not waiting for pickup.";
    public override string MarkPickupFirst => "Mark the pickup first.";
    public override string PickupFrom(int hours) => $"You can mark the pickup from {hours} hours before the pickup time.";
    public override string DriverLinkInvalid => "This link is not valid. Ask the dispatcher for a new one.";

    public override string Dear(string name) => $"Dear {name},";
    public override string KindRegards => "Kind regards,";
    public override string PaymentConfirmed(string amount) => $"thank you, your payment of {amount} is confirmed and your ride is booked.";
    public override string Booking => "Booking";
    public override string Pickup => "Pickup";
    public override string ItalianTime => "(Italian time)";
    public override string From => "From";
    public override string To => "To";
    public override string Vehicle => "Vehicle";
    public override string VehicleSummary(VehicleCategory category, int passengers, int large, int small) =>
        $"{Category(category)}, {passengers} passengers, {large} large + {small} small bags";
    public override string Flight => "Flight";
    public override string MeetingPoint(string instructions) => $"Meeting point: {instructions}";
    public override string DriverDetailsFollow => "Your driver's name and car will be sent to you before the ride.";
    public override string ManageBooking => "View or manage your booking";
    public override string ViewBooking => "View your booking";
    public override string InvoiceAttached(string number) => $"The invoice {number} is attached.";
    public override string ConfirmedSubject(string reference) => $"Booking confirmed – {reference}";
    public override string BookingCancelled(string reference) => $"your booking {reference} has been cancelled.";
    public override string Refunded(string amount) =>
        $"We have refunded {amount} to your original payment method. Depending on your bank it can take 5–10 days to appear.";
    public override string CreditNoteAttached(string number) => $"The credit note {number} is attached.";
    public override string CancelledSubject(string reference) => $"Booking cancelled and refunded – {reference}";
    public override string RefundSubject(string reference) => $"Refund – {reference}";
    public override string Transfer(string reference, DateTime local, string from, string to) =>
        $"Transfer {reference}, {local.ToString("dd/MM/yyyy HH:mm", Culture)}: {from} → {to}";
    public override string RefundForInvoice(string number) => $"Refund for invoice {number}";
    public override string DriverSubject(string reference) => $"Your driver for {reference}";
    public override string YourDriver(string pickup, string where, string driver, string phone) =>
        $"your driver for {pickup} at {where} is {driver} ({phone}).";
    public override string Car(string car, string plate) => $"Car: {car}, plate {plate}.";
    public override string ReviewSubject(string reference) => $"How was your ride {reference}?";
    public override string ReviewAsk => "thank you for travelling with us. Tell us how the ride and the driver were — it takes a minute.";
    public override string RateRide => "Rate your ride";

    public override string Hello(string name) => $"Hello {name},";
    public override string ShiftCancelled(string when) => $"Your shift of {when} was cancelled.";
    public override string ShiftChanged(string when) => $"Your shift of {when} was changed. Please check and confirm it again.";
    public override string NewShift(string when, int rides, string plate) =>
        $"New shift on {when}: {rides} {(rides == 1 ? "ride" : "rides")} with {plate}. Please confirm it.";
    public override string OpenMyShifts => "Open my shifts";
}
