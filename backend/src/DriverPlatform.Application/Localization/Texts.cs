using System.Globalization;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Localization;

/// <summary>
/// What clients, drivers and staff read from the backend, in the languages of the site: emails,
/// price lines and error messages (the admin ones are in Texts.Admin.cs). Each language is a
/// subclass, so a missing text does not compile.
/// </summary>
public abstract partial class Texts
{
    public static readonly Texts English = new EnglishTexts();
    public static readonly Texts Italian = new ItalianTexts();
    public static readonly Texts German = new GermanTexts();
    public static readonly Texts French = new FrenchTexts();

    public static IReadOnlyList<Texts> All { get; } = [Italian, English, German, French];

    /// <summary>"it", "de", "fr" or "en"; any other language (Spanish, Russian…) gets English.</summary>
    public static Texts For(string? language) => language?.Trim().ToLowerInvariant() switch
    {
        "it" => Italian,
        "de" => German,
        "fr" => French,
        _ => English,
    };

    /// <summary>The language of the current API request (its Accept-Language header); English when there is none.</summary>
    public static Texts Current => For(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    /// <summary>"it", "en", "de" or "fr".</summary>
    public abstract string Language { get; }

    /// <summary>Formats dates and amounts: "58,00 €" in Italian, German and French, "€58.00" in English.</summary>
    public abstract CultureInfo Culture { get; }

    public string Money(decimal amount) => amount.ToString("C2", Culture);

    /// <summary>"Monday 3 May 2027, 14:30".</summary>
    public string LongDateTime(DateTime local) => local.ToString(LongDateTimeFormat, Culture);

    /// <summary>"Mon 3 May, 14:30".</summary>
    public string ShortDateTime(DateTime local) => local.ToString(ShortDateTimeFormat, Culture);

    protected virtual string LongDateTimeFormat => "dddd d MMMM yyyy, HH:mm";
    protected virtual string ShortDateTimeFormat => "ddd d MMM, HH:mm";

    // Price lines. They are stored on the booking in the language it was made in.

    public abstract string Category(VehicleCategory category);
    public string FareLine(VehicleCategory category, decimal km) => $"{Category(category)} · {km.ToString("0.0", Culture)} km";
    public string SurchargeLine(string name, decimal percent) => $"{name} +{percent.ToString("0.##", Culture)}%";
    public abstract string ReturnDiscount(decimal percent);
    public abstract string ZoneAccess(string zone);
    public abstract string ChildSeat { get; }
    public abstract string ExtraLuggage { get; }
    public abstract string NameSign { get; }
    public abstract string PublicHolidays { get; }
    public abstract string UpToPassengers(int count);
    public abstract string UpToLargeBags(int count);

    // Errors shown on the booking form, the client's booking page and the review page.

    public abstract string AcceptPrivacyToBook { get; }
    public abstract string AcceptPrivacy { get; }
    public abstract string CategoryNotOffered { get; }
    public abstract string VehicleDoesNotFit { get; }
    public abstract string PriceChanged { get; }
    public abstract string BookingWasCancelled { get; }
    public abstract string BookingAlreadyPaid { get; }
    public abstract string PayTheOutbound { get; }
    public abstract string ReturnAfterArrival { get; }
    public abstract string ChildSeatLimit { get; }
    public abstract string ChildSeatGroupOnce { get; }
    public abstract string OutsideServiceArea { get; }
    public abstract string TooClose { get; }
    public abstract string PickupTimeRequired { get; }
    public abstract string TimeDoesNotExist { get; }
    public abstract string BookAhead(int hours);
    public abstract string OneYearAhead { get; }
    public abstract string NoMeetingPointYet(string zone);
    public abstract string NotAMeetingPoint(string requested, string zone);
    public abstract string BookingNotFound { get; }
    public abstract string InvoiceNotFound { get; }
    public abstract string AlreadyRated { get; }
    public abstract string ReviewLinkInvalid { get; }

    // The driver's page.

    public abstract string ShiftNotFound { get; }
    public abstract string ShiftAlreadyConfirmed { get; }
    public abstract string ShiftCannotBeConfirmed { get; }
    public abstract string RideNotFound { get; }
    public abstract string ConfirmShiftFirst { get; }
    public abstract string NotWaitingForPickup { get; }
    public abstract string MarkPickupFirst { get; }
    public abstract string PickupFrom(int hours);
    public abstract string DriverLinkInvalid { get; }

    // Emails to clients.

    public abstract string Dear(string name);
    public abstract string KindRegards { get; }
    public abstract string PaymentConfirmed(string amount);
    public abstract string Booking { get; }
    public abstract string Pickup { get; }
    public abstract string ItalianTime { get; }
    public abstract string From { get; }
    public abstract string To { get; }
    public abstract string Vehicle { get; }
    public abstract string VehicleSummary(VehicleCategory category, int passengers, int large, int small);
    public abstract string Flight { get; }
    public abstract string MeetingPoint(string instructions);
    public abstract string DriverDetailsFollow { get; }
    public abstract string ManageBooking { get; }
    public abstract string ViewBooking { get; }
    public abstract string InvoiceAttached(string number);
    public abstract string ConfirmedSubject(string reference);
    public abstract string BookingCancelled(string reference);
    public abstract string Refunded(string amount);
    public abstract string CreditNoteAttached(string number);
    public abstract string CancelledSubject(string reference);
    public abstract string RefundSubject(string reference);
    /// <summary>One ride on one line: emails, invoices.</summary>
    public abstract string Transfer(string reference, DateTime local, string from, string to);
    public abstract string RefundForInvoice(string number);
    public abstract string DriverSubject(string reference);
    public abstract string YourDriver(string pickup, string where, string driver, string phone);
    public abstract string Car(string car, string plate);
    public abstract string ReviewSubject(string reference);
    public abstract string ReviewAsk { get; }
    public abstract string RateRide { get; }

    // Messages to drivers.

    public abstract string Hello(string name);
    public abstract string ShiftCancelled(string when);
    public abstract string ShiftChanged(string when);
    public abstract string NewShift(string when, int rides, string plate);
    public abstract string OpenMyShifts { get; }
}
