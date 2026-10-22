using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Localization;

internal sealed partial class FrenchTexts
{
    public override string BookingStatusName(BookingStatus status) => status switch
    {
        BookingStatus.AwaitingPayment => "« en attente de paiement »",
        BookingStatus.New => "« à attribuer »",
        BookingStatus.Assigned => "« attribuée »",
        BookingStatus.PickedUp => "« prise en charge »",
        BookingStatus.Completed => "« terminée »",
        _ => "« annulée »",
    };
    public override string ShiftStatusName(AssignmentStatus status) => status switch
    {
        AssignmentStatus.Draft => "« brouillon »",
        AssignmentStatus.Sent => "« envoyé au chauffeur »",
        AssignmentStatus.Seen => "« vu par le chauffeur »",
        AssignmentStatus.Confirmed => "« confirmé »",
        AssignmentStatus.Completed => "« terminé »",
        _ => "« annulé »",
    };
    public override string BookingStatusChange(BookingStatus from, BookingStatus to) =>
        $"Une réservation au statut {BookingStatusName(from)} ne peut pas passer à {BookingStatusName(to)}.";
    public override string ShiftStatusChange(AssignmentStatus from, AssignmentStatus to) =>
        $"Un service au statut {ShiftStatusName(from)} ne peut pas passer à {ShiftStatusName(to)}.";
    public override string ShiftLocked(AssignmentStatus status) => $"Un service au statut {ShiftStatusName(status)} ne peut pas être modifié.";
    public override string SayWhyBookingCancelled => "Indiquez pourquoi la réservation est annulée.";
    public override string SayWhyShiftCancelled => "Indiquez pourquoi le service est annulé.";
    public override string AlreadyPickedUp => "Le chauffeur a déjà pris en charge un client dans ce service.";
    public override string ChooseActiveDriver => "Choisissez un chauffeur actif.";
    public override string ChooseActiveVehicle => "Choisissez un véhicule actif.";
    public override string ChooseBookings => "Choisissez une ou plusieurs réservations existantes.";
    public override string StartsBeforeEnds(string later, string earlier) => $"{later} commence avant la fin de {earlier}.";
    public override string TightGap(int minutes, string earlier, string later) =>
        $"Seulement {minutes} min entre {earlier} et {later}, plus le trajet jusqu'à la prise en charge suivante.";
    public override string MarkedNotAvailable => "Indiqué comme indisponible";
    public override string OnAnotherShift => "Déjà dans un autre service à cette heure";

    public override string DocumentName(DriverDocumentType type) => type switch
    {
        DriverDocumentType.DrivingLicence => "Permis de conduire",
        DriverDocumentType.NccLicence => "Licence NCC",
        DriverDocumentType.TaxiLicence => "Licence de taxi",
        DriverDocumentType.ProfessionalCertificate => "Certificat professionnel (CAP/KB)",
        DriverDocumentType.Insurance => "Assurance",
        _ => "Casier judiciaire",
    };
    public override string VehicleInsurance => "Assurance";
    public override string VehicleInspection => "Contrôle technique (revisione)";
    public override string DocumentExpired(string document, DateOnly date) => $"{document} expiré le {date.ToString("dd/MM/yyyy", Culture)}";
    public override string NoDrivingLicence => "Aucun permis de conduire enregistré";
    public override string NoNccOrTaxiLicence => "Aucune licence NCC ou taxi enregistrée";
    public override string NoInsuranceDate => "Aucune date d'assurance enregistrée";
    public override string NoInspectionDate => "Aucune date de contrôle technique enregistrée";

    public override string DriverNotFound => "Chauffeur introuvable.";
    public override string PhoneTaken => "Un autre chauffeur a déjà ce numéro de téléphone.";
    public override string VehicleNotFound => "Véhicule introuvable.";
    public override string PlateTaken => "Un autre véhicule a déjà cette immatriculation.";
    public override string ZoneNotFound => "Zone introuvable.";
    public override string PickCentre => "Choisissez le centre sur la carte et indiquez un rayon.";
    public override string PolygonOutsideServiceArea => "Tous les points doivent être dans la zone desservie (Italie, France, Allemagne, Suisse).";
    public override string MeetingPointNameAndAddress => "Le nom et l'adresse sont obligatoires.";
    public override string PlaceMeetingPoint => "Placez le point de rendez-vous sur la carte.";
    public override string MeetingPointNamesUnique => "Les noms des points de rendez-vous doivent être uniques dans la zone.";
    public override string SurchargeNotFound => "Majoration introuvable.";
    public override string PickADay => "Choisissez au moins un jour.";
    public override string EnterBothDates => "Indiquez les deux dates.";
    public override string EndBeforeStart => "La date de fin est antérieure à la date de début.";
    public override string EnterBothTimes => "Indiquez les deux heures.";
    public override string SameStartAndEnd => "L'heure de début et de fin sont identiques.";
    public override string PartnerNotFound => "Partenaire introuvable.";
    public override string HttpsOnly => "Utilisez une adresse https://.";
    public override string PaymentNotFound => "Paiement introuvable.";
    public override string OnlyPaidRefundable => "Seuls les paiements réussis peuvent être remboursés.";
    public override string RefundRange(string max) => $"Remboursement entre {Money(0.01m)} et {max}.";
    public override string ApplicationNotFound => "Candidature introuvable.";

    public override string EmailTaken => "Un compte existe déjà avec cet e-mail.";
    public override string LastAdmin => "C'est le dernier administrateur actif. Nommez d'abord quelqu'un d'autre administrateur.";
    public override string CannotDeactivateSelf => "Vous ne pouvez pas désactiver votre propre compte.";
    public override string UserNotFound => "Utilisateur introuvable.";
    public override string PasswordTooShort(int length) => $"Utilisez au moins {length} caractères ; une courte phrase se retient facilement.";
    public override string PasswordTooEasy => "Ce mot de passe est trop facile à deviner.";
    public override string CurrentPasswordWrong => "Le mot de passe actuel est incorrect.";
    public override string ChooseDifferentPassword => "Choisissez un autre mot de passe.";
}
