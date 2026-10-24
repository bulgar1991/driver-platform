using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Localization;

internal sealed partial class GermanTexts
{
    public override string BookingStatusName(BookingStatus status) => status switch
    {
        BookingStatus.AwaitingPayment => "„Zahlung ausstehend“",
        BookingStatus.New => "„zuzuweisen“",
        BookingStatus.Assigned => "„zugewiesen“",
        BookingStatus.PickedUp => "„abgeholt“",
        BookingStatus.Completed => "„abgeschlossen“",
        _ => "„storniert“",
    };
    public override string ShiftStatusName(AssignmentStatus status) => status switch
    {
        AssignmentStatus.Draft => "„Entwurf“",
        AssignmentStatus.Sent => "„an Fahrer gesendet“",
        AssignmentStatus.Seen => "„vom Fahrer gesehen“",
        AssignmentStatus.Confirmed => "„bestätigt“",
        AssignmentStatus.Completed => "„abgeschlossen“",
        _ => "„storniert“",
    };
    public override string BookingStatusChange(BookingStatus from, BookingStatus to) =>
        $"Eine Buchung mit Status {BookingStatusName(from)} kann nicht zu {BookingStatusName(to)} wechseln.";
    public override string ShiftStatusChange(AssignmentStatus from, AssignmentStatus to) =>
        $"Eine Schicht mit Status {ShiftStatusName(from)} kann nicht zu {ShiftStatusName(to)} wechseln.";
    public override string ShiftLocked(AssignmentStatus status) => $"Eine Schicht mit Status {ShiftStatusName(status)} kann nicht geändert werden.";
    public override string SayWhyBookingCancelled => "Geben Sie an, warum die Buchung storniert wird.";
    public override string SayWhyShiftCancelled => "Geben Sie an, warum die Schicht storniert wird.";
    public override string AlreadyPickedUp => "Der Fahrer hat in dieser Schicht bereits einen Kunden abgeholt.";
    public override string ChooseActiveDriver => "Wählen Sie einen aktiven Fahrer.";
    public override string ChooseActiveVehicle => "Wählen Sie ein aktives Fahrzeug.";
    public override string ChooseBookings => "Wählen Sie eine oder mehrere bestehende Buchungen.";
    public override string StartsBeforeEnds(string later, string earlier) => $"{later} beginnt, bevor {earlier} endet.";
    public override string TightGap(int minutes, string earlier, string later) =>
        $"Nur {minutes} Min. zwischen {earlier} und {later}, plus die Fahrt zur nächsten Abholung.";
    public override string MarkedNotAvailable => "Als nicht verfügbar markiert";
    public override string OnAnotherShift => "Zu dieser Zeit bereits in einer anderen Schicht";

    public override string DocumentName(DriverDocumentType type) => type switch
    {
        DriverDocumentType.DrivingLicence => "Führerschein",
        DriverDocumentType.NccLicence => "NCC-Lizenz",
        DriverDocumentType.TaxiLicence => "Taxilizenz",
        DriverDocumentType.ProfessionalCertificate => "Berufsbescheinigung (CAP/KB)",
        DriverDocumentType.Insurance => "Versicherung",
        _ => "Führungszeugnis",
    };
    public override string VehicleInsurance => "Versicherung";
    public override string VehicleInspection => "Hauptuntersuchung (revisione)";
    public override string DocumentExpired(string document, DateOnly date) => $"{document} abgelaufen am {date.ToString("dd.MM.yyyy", Culture)}";
    public override string NoDrivingLicence => "Kein Führerschein hinterlegt";
    public override string NoNccOrTaxiLicence => "Keine NCC- oder Taxilizenz hinterlegt";
    public override string NoInsuranceDate => "Kein Versicherungsdatum hinterlegt";
    public override string NoInspectionDate => "Kein Datum der Hauptuntersuchung hinterlegt";

    public override string DriverNotFound => "Fahrer nicht gefunden.";
    public override string PhoneTaken => "Ein anderer Fahrer hat bereits diese Telefonnummer.";
    public override string VehicleNotFound => "Fahrzeug nicht gefunden.";
    public override string PlateTaken => "Ein anderes Fahrzeug hat bereits dieses Kennzeichen.";
    public override string VehicleHasShifts => "Dieses Fahrzeug war bereits in Schichten eingeplant und kann nicht gelöscht werden. Entfernen Sie stattdessen das Häkchen bei Aktiv.";
    public override string DriverHasShifts => "Dieser Fahrer hat Schichten oder Bewertungen und kann nicht gelöscht werden. Entfernen Sie stattdessen das Häkchen bei Aktiv.";
    public override string PhotoTooLarge => "Das Bild ist größer als 5 MB.";
    public override string PhotoNotAnImage => "Wählen Sie ein JPEG-, PNG- oder WebP-Bild.";
    public override string ZoneNotFound => "Zone nicht gefunden.";
    public override string PickCentre => "Wählen Sie den Mittelpunkt auf der Karte und legen Sie einen Radius fest.";
    public override string PolygonOutsideServiceArea => "Alle Punkte müssen im Einsatzgebiet liegen (Italien, Frankreich, Deutschland, Schweiz).";
    public override string MeetingPointNameAndAddress => "Name und Adresse sind erforderlich.";
    public override string PlaceMeetingPoint => "Platzieren Sie den Treffpunkt auf der Karte.";
    public override string MeetingPointNamesUnique => "Die Namen der Treffpunkte müssen in der Zone eindeutig sein.";
    public override string SurchargeNotFound => "Zuschlag nicht gefunden.";
    public override string PickADay => "Wählen Sie mindestens einen Tag.";
    public override string EnterBothDates => "Geben Sie beide Daten an.";
    public override string EndBeforeStart => "Das Enddatum liegt vor dem Startdatum.";
    public override string EnterBothTimes => "Geben Sie beide Uhrzeiten an.";
    public override string SameStartAndEnd => "Start- und Endzeit sind gleich.";
    public override string PartnerNotFound => "Partner nicht gefunden.";
    public override string HttpsOnly => "Verwenden Sie eine https://-Adresse.";
    public override string PaymentNotFound => "Zahlung nicht gefunden.";
    public override string OnlyPaidRefundable => "Nur bezahlte Zahlungen können erstattet werden.";
    public override string RefundRange(string max) => $"Erstattung zwischen {Money(0.01m)} und {max}.";
    public override string ApplicationNotFound => "Bewerbung nicht gefunden.";

    public override string EmailTaken => "Es gibt bereits ein Konto mit dieser E-Mail.";
    public override string LastAdmin => "Dies ist der letzte aktive Administrator. Machen Sie zuerst jemand anderen zum Administrator.";
    public override string CannotDeactivateSelf => "Sie können Ihr eigenes Konto nicht deaktivieren.";
    public override string UserNotFound => "Benutzer nicht gefunden.";
    public override string PasswordTooShort(int length) => $"Verwenden Sie mindestens {length} Zeichen; ein kurzer Satz ist leicht zu merken.";
    public override string PasswordTooEasy => "Dieses Passwort ist zu leicht zu erraten.";
    public override string CurrentPasswordWrong => "Das aktuelle Passwort ist nicht korrekt.";
    public override string ChooseDifferentPassword => "Wählen Sie ein anderes Passwort.";
}
