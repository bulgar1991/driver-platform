using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Localization;

internal sealed partial class ItalianTexts
{
    public override string BookingStatusName(BookingStatus status) => status switch
    {
        BookingStatus.AwaitingPayment => "«in attesa di pagamento»",
        BookingStatus.New => "«da assegnare»",
        BookingStatus.Assigned => "«assegnata»",
        BookingStatus.PickedUp => "«a bordo»",
        BookingStatus.Completed => "«completata»",
        _ => "«annullata»",
    };
    public override string ShiftStatusName(AssignmentStatus status) => status switch
    {
        AssignmentStatus.Draft => "«in bozza»",
        AssignmentStatus.Sent => "«inviato all'autista»",
        AssignmentStatus.Seen => "«visto dall'autista»",
        AssignmentStatus.Confirmed => "«confermato»",
        AssignmentStatus.Completed => "«completato»",
        _ => "«annullato»",
    };
    public override string BookingStatusChange(BookingStatus from, BookingStatus to) =>
        $"Una prenotazione {BookingStatusName(from)} non può passare a {BookingStatusName(to)}.";
    public override string ShiftStatusChange(AssignmentStatus from, AssignmentStatus to) =>
        $"Un turno {ShiftStatusName(from)} non può passare a {ShiftStatusName(to)}.";
    public override string ShiftLocked(AssignmentStatus status) => $"Un turno {ShiftStatusName(status)} non si può modificare.";
    public override string SayWhyBookingCancelled => "Indica perché la prenotazione è annullata.";
    public override string SayWhyShiftCancelled => "Indica perché il turno è annullato.";
    public override string AlreadyPickedUp => "L'autista ha già preso a bordo un cliente in questo turno.";
    public override string ChooseActiveDriver => "Scegli un autista attivo.";
    public override string ChooseActiveVehicle => "Scegli un veicolo attivo.";
    public override string ChooseBookings => "Scegli una o più prenotazioni esistenti.";
    public override string StartsBeforeEnds(string later, string earlier) => $"{later} inizia prima che finisca {earlier}.";
    public override string TightGap(int minutes, string earlier, string later) =>
        $"Solo {minutes} min tra {earlier} e {later}, più il tragitto fino alla partenza successiva.";
    public override string MarkedNotAvailable => "Segnato come non disponibile";
    public override string OnAnotherShift => "Già in un altro turno a quest'ora";

    public override string DocumentName(DriverDocumentType type) => type switch
    {
        DriverDocumentType.DrivingLicence => "Patente di guida",
        DriverDocumentType.NccLicence => "Licenza NCC",
        DriverDocumentType.TaxiLicence => "Licenza taxi",
        DriverDocumentType.ProfessionalCertificate => "Certificato professionale (CAP/KB)",
        DriverDocumentType.Insurance => "Assicurazione",
        _ => "«Casellario giudiziale»",
    };
    public override string VehicleInsurance => "Assicurazione";
    public override string VehicleInspection => "Revisione";
    public override string DocumentExpired(string document, DateOnly date) => $"{document} scaduto il {date.ToString("dd/MM/yyyy", Culture)}";
    public override string NoDrivingLicence => "Nessuna patente registrata";
    public override string NoNccOrTaxiLicence => "Nessuna licenza NCC o taxi registrata";
    public override string NoInsuranceDate => "Nessuna data di assicurazione registrata";
    public override string NoInspectionDate => "Nessuna data di revisione registrata";

    public override string DriverNotFound => "Autista non trovato.";
    public override string PhoneTaken => "Un altro autista ha già questo numero di telefono.";
    public override string VehicleNotFound => "Veicolo non trovato.";
    public override string PlateTaken => "Un altro veicolo ha già questa targa.";
    public override string VehicleHasShifts => "Questo veicolo è stato usato nei turni e non può essere eliminato. Togli la spunta da Attivo.";
    public override string DriverHasShifts => "Questo autista ha turni o recensioni e non può essere eliminato. Togli la spunta da Attivo.";
    public override string PhotoTooLarge => "L'immagine supera i 5 MB.";
    public override string PhotoNotAnImage => "Scegli un'immagine JPEG, PNG o WebP.";
    public override string ZoneNotFound => "Zona non trovata.";
    public override string PickCentre => "Scegli il centro sulla mappa e imposta un raggio.";
    public override string PolygonOutsideServiceArea => "Tutti i punti devono essere nell'area servita (Italia, Francia, Germania, Svizzera).";
    public override string MeetingPointNameAndAddress => "Nome e indirizzo sono obbligatori.";
    public override string PlaceMeetingPoint => "Posiziona il punto d'incontro sulla mappa.";
    public override string MeetingPointNamesUnique => "I nomi dei punti d'incontro devono essere unici nella zona.";
    public override string SurchargeNotFound => "Supplemento non trovato.";
    public override string PickADay => "Scegli almeno un giorno.";
    public override string EnterBothDates => "Inserisci entrambe le date.";
    public override string EndBeforeStart => "La data di fine è prima della data di inizio.";
    public override string EnterBothTimes => "Inserisci entrambi gli orari.";
    public override string SameStartAndEnd => "Ora di inizio e di fine coincidono.";
    public override string PartnerNotFound => "Partner non trovato.";
    public override string HttpsOnly => "Usa un indirizzo https://.";
    public override string PaymentNotFound => "Pagamento non trovato.";
    public override string OnlyPaidRefundable => "Si possono rimborsare solo i pagamenti riusciti.";
    public override string RefundRange(string max) => $"Rimborso tra {Money(0.01m)} e {max}.";
    public override string ApplicationNotFound => "Candidatura non trovata.";

    public override string EmailTaken => "Esiste già un account con questa email.";
    public override string LastAdmin => "È l'ultimo amministratore attivo. Rendi prima amministratore qualcun altro.";
    public override string CannotDeactivateSelf => "Non puoi disattivare il tuo account.";
    public override string UserNotFound => "Utente non trovato.";
    public override string PasswordTooShort(int length) => $"Usa almeno {length} caratteri; una breve frase si ricorda facilmente.";
    public override string PasswordTooEasy => "Questa password è troppo facile da indovinare.";
    public override string CurrentPasswordWrong => "La password attuale non è corretta.";
    public override string ChooseDifferentPassword => "Scegli una password diversa.";
}
