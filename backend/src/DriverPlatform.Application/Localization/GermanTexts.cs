using System.Globalization;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Localization;

/// <summary>Formal "Sie", as on the site.</summary>
internal sealed partial class GermanTexts : Texts
{
    public override string Language => "de";
    public override CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("de-DE");

    protected override string LongDateTimeFormat => "dddd, d. MMMM yyyy, HH:mm";
    protected override string ShortDateTimeFormat => "ddd, d. MMM, HH:mm";

    public override string Category(VehicleCategory category) => category switch
    {
        VehicleCategory.Sedan => "Limousine",
        VehicleCategory.Estate => "Kombi / SUV",
        _ => "Van",
    };
    public override string ReturnDiscount(decimal percent) => $"Rückfahrt −{percent.ToString("0.##", Culture)}%";
    public override string ZoneAccess(string zone) => $"Zufahrt {zone}";
    public override string ChildSeat => "Kindersitz";
    public override string ExtraLuggage => "Zusätzliches Gepäck";
    public override string NameSign => "Namensschild";
    public override string PublicHolidays => "Feiertage";
    public override string UpToPassengers(int count) => $"Bis zu {count} Fahrgäste";
    public override string UpToLargeBags(int count) => $"Bis zu {count} große Gepäckstücke";

    public override string AcceptPrivacyToBook => "Bitte akzeptieren Sie die Datenschutzerklärung, um zu buchen.";
    public override string AcceptPrivacy => "Bitte akzeptieren Sie die Datenschutzerklärung.";
    public override string CategoryNotOffered => "Diese Fahrzeugkategorie wird nicht angeboten.";
    public override string VehicleDoesNotFit => "Dieses Fahrzeug passt nicht zu der Fahrt.";
    public override string PriceChanged => "Der Preis hat sich geändert. Bitte prüfen Sie den neuen Preis und bestätigen Sie erneut.";
    public override string BookingWasCancelled => "Diese Buchung wurde storniert.";
    public override string BookingAlreadyPaid => "Diese Buchung ist bereits bezahlt.";
    public override string PayTheOutbound => "Bezahlen Sie die Hinfahrt; die Rückfahrt ist enthalten.";
    public override string ReturnAfterArrival => "Die Rückfahrt muss nach der Ankunft der ersten Fahrt beginnen.";
    public override string ChildSeatLimit => "Höchstens ein Kindersitz pro Fahrgast und drei pro Fahrzeug.";
    public override string ChildSeatGroupOnce => "Geben Sie jede Kindersitzgruppe nur einmal an.";
    public override string OutsideServiceArea => "Wir fahren in Italien, Frankreich, Deutschland und der Schweiz.";
    public override string TooClose => "Abhol- und Zielort liegen zu nah beieinander.";
    public override string PickupTimeRequired => "Bitte geben Sie Abholdatum und -uhrzeit ein.";
    public override string TimeDoesNotExist => "Diese Uhrzeit gibt es nicht (Zeitumstellung). Bitte wählen Sie eine andere.";
    public override string BookAhead(int hours) => $"Bitte buchen Sie mindestens {hours} Stunden im Voraus.";
    public override string OneYearAhead => "Buchungen sind bis zu einem Jahr im Voraus möglich.";
    public override string NoMeetingPointYet(string zone) => $"Für {zone} ist noch kein Treffpunkt eingerichtet. Bitte kontaktieren Sie uns.";
    public override string NotAMeetingPoint(string requested, string zone) => $"„{requested}“ ist kein Treffpunkt von {zone}.";
    public override string BookingNotFound => "Buchung nicht gefunden.";
    public override string InvoiceNotFound => "Rechnung nicht gefunden.";
    public override string AlreadyRated => "Vielen Dank, diese Fahrt wurde bereits bewertet.";
    public override string ReviewLinkInvalid => "Dieser Bewertungslink ist ungültig oder abgelaufen.";

    public override string ShiftNotFound => "Schicht nicht gefunden.";
    public override string ShiftAlreadyConfirmed => "Bereits bestätigt.";
    public override string ShiftCannotBeConfirmed => "Diese Schicht kann nicht mehr bestätigt werden.";
    public override string RideNotFound => "Fahrt nicht gefunden.";
    public override string ConfirmShiftFirst => "Bestätigen Sie zuerst die Schicht.";
    public override string NotWaitingForPickup => "Diese Fahrt wartet nicht auf die Abholung.";
    public override string MarkPickupFirst => "Markieren Sie zuerst die Abholung.";
    public override string PickupFrom(int hours) => $"Sie können die Abholung ab {hours} Stunden vor der Abholzeit markieren.";
    public override string DriverLinkInvalid => "Dieser Link ist ungültig. Bitten Sie die Disposition um einen neuen.";

    public override string Dear(string name) => $"Guten Tag {name},";
    public override string KindRegards => "Mit freundlichen Grüßen";
    public override string PaymentConfirmed(string amount) => $"vielen Dank, Ihre Zahlung von {amount} ist bestätigt und Ihre Fahrt ist gebucht.";
    public override string Booking => "Buchung";
    public override string Pickup => "Abholung";
    public override string ItalianTime => "(italienische Zeit)";
    public override string From => "Von";
    public override string To => "Nach";
    public override string Vehicle => "Fahrzeug";
    public override string VehicleSummary(VehicleCategory category, int passengers, int large, int small) =>
        $"{Category(category)}, {passengers} {(passengers == 1 ? "Fahrgast" : "Fahrgäste")}, {large} große + {small} kleine Gepäckstücke";
    public override string Flight => "Flug";
    public override string MeetingPoint(string instructions) => $"Treffpunkt: {instructions}";
    public override string DriverDetailsFollow => "Name und Fahrzeug Ihres Fahrers erhalten Sie vor der Fahrt.";
    public override string ManageBooking => "Buchung ansehen oder verwalten";
    public override string ViewBooking => "Buchung ansehen";
    public override string InvoiceAttached(string number) => $"Die Rechnung {number} finden Sie im Anhang.";
    public override string ConfirmedSubject(string reference) => $"Buchung bestätigt – {reference}";
    public override string BookingCancelled(string reference) => $"Ihre Buchung {reference} wurde storniert.";
    public override string Refunded(string amount) =>
        $"Wir haben {amount} auf Ihr ursprüngliches Zahlungsmittel erstattet. Je nach Bank kann es 5–10 Tage dauern, bis der Betrag erscheint.";
    public override string CreditNoteAttached(string number) => $"Die Gutschrift {number} finden Sie im Anhang.";
    public override string CancelledSubject(string reference) => $"Buchung storniert und erstattet – {reference}";
    public override string RefundSubject(string reference) => $"Erstattung – {reference}";
    public override string Transfer(string reference, DateTime local, string from, string to) =>
        $"Transfer {reference}, {local.ToString("dd.MM.yyyy HH:mm", Culture)}: {from} → {to}";
    public override string RefundForInvoice(string number) => $"Erstattung zur Rechnung {number}";
    public override string DriverSubject(string reference) => $"Ihr Fahrer für {reference}";
    public override string YourDriver(string pickup, string where, string driver, string phone) =>
        $"Ihr Fahrer für {pickup} in {where} ist {driver} ({phone}).";
    public override string Car(string car, string plate) => $"Fahrzeug: {car}, Kennzeichen {plate}.";
    public override string ReviewSubject(string reference) => $"Wie war Ihre Fahrt {reference}?";
    public override string ReviewAsk => "vielen Dank, dass Sie mit uns gefahren sind. Sagen Sie uns, wie Fahrt und Fahrer waren – es dauert nur eine Minute.";
    public override string RateRide => "Fahrt bewerten";

    public override string Hello(string name) => $"Guten Tag {name},";
    public override string ShiftCancelled(string when) => $"Ihre Schicht am {when} wurde storniert.";
    public override string ShiftChanged(string when) => $"Ihre Schicht am {when} wurde geändert. Bitte prüfen und bestätigen Sie sie erneut.";
    public override string NewShift(string when, int rides, string plate) =>
        $"Neue Schicht am {when}: {rides} {(rides == 1 ? "Fahrt" : "Fahrten")} mit {plate}. Bitte bestätigen Sie sie.";
    public override string OpenMyShifts => "Meine Schichten öffnen";
}
