using System.Globalization;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Localization;

/// <summary>Formal "vous", as on the site.</summary>
internal sealed class FrenchTexts : Texts
{
    public override string Language => "fr";
    public override CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("fr-FR");

    public override string Category(VehicleCategory category) => category switch
    {
        VehicleCategory.Sedan => "Berline",
        VehicleCategory.Estate => "Break / SUV",
        _ => "Van",
    };
    public override string ReturnDiscount(decimal percent) => $"Trajet retour −{percent.ToString("0.##", Culture)} %";
    public override string ZoneAccess(string zone) => $"Accès {zone}";
    public override string ChildSeat => "Siège enfant";
    public override string ExtraLuggage => "Bagage supplémentaire";
    public override string NameSign => "Pancarte nominative";
    public override string PublicHolidays => "Jours fériés";
    public override string UpToPassengers(int count) => $"Jusqu'à {count} passagers";
    public override string UpToLargeBags(int count) => $"Jusqu'à {count} grands bagages";

    public override string AcceptPrivacyToBook => "Veuillez accepter la politique de confidentialité pour réserver.";
    public override string AcceptPrivacy => "Veuillez accepter la politique de confidentialité.";
    public override string CategoryNotOffered => "Cette catégorie de véhicule n'est pas proposée.";
    public override string VehicleDoesNotFit => "Ce véhicule ne convient pas à ce trajet.";
    public override string PriceChanged => "Le prix a changé. Veuillez vérifier le nouveau prix et confirmer à nouveau.";
    public override string BookingWasCancelled => "Cette réservation a été annulée.";
    public override string BookingAlreadyPaid => "Cette réservation est déjà payée.";
    public override string PayTheOutbound => "Payez la réservation aller ; le retour est inclus.";
    public override string ReturnAfterArrival => "Le retour doit partir après l'arrivée du premier trajet.";
    public override string ChildSeatLimit => "Au maximum un siège enfant par passager et trois par véhicule.";
    public override string ChildSeatGroupOnce => "Indiquez chaque groupe de siège enfant une seule fois.";
    public override string OutsideServiceArea => "Nous opérons en Italie, en France, en Allemagne et en Suisse.";
    public override string TooClose => "Le départ et l'arrivée sont trop proches.";
    public override string TimeDoesNotExist => "Cette heure n'existe pas (passage à l'heure d'été). Choisissez une autre heure.";
    public override string BookAhead(int hours) => $"Réservez au moins {hours} heures à l'avance.";
    public override string OneYearAhead => "Les réservations sont ouvertes jusqu'à un an à l'avance.";
    public override string NoMeetingPointYet(string zone) => $"{zone} n'a pas encore de point de rendez-vous. Veuillez nous contacter.";
    public override string NotAMeetingPoint(string requested, string zone) => $"« {requested} » n'est pas un point de rendez-vous de {zone}.";
    public override string BookingNotFound => "Réservation introuvable.";
    public override string InvoiceNotFound => "Facture introuvable.";
    public override string AlreadyRated => "Merci, ce trajet a déjà été évalué.";
    public override string ReviewLinkInvalid => "Ce lien d'avis n'est pas valide ou a expiré.";

    public override string ShiftNotFound => "Service introuvable.";
    public override string ShiftAlreadyConfirmed => "Déjà confirmé.";
    public override string ShiftCannotBeConfirmed => "Ce service ne peut plus être confirmé.";
    public override string RideNotFound => "Trajet introuvable.";
    public override string ConfirmShiftFirst => "Confirmez d'abord le service.";
    public override string NotWaitingForPickup => "Ce trajet n'attend pas de prise en charge.";
    public override string MarkPickupFirst => "Indiquez d'abord la prise en charge.";
    public override string PickupFrom(int hours) => $"Vous pouvez indiquer la prise en charge à partir de {hours} heures avant l'heure prévue.";
    public override string DriverLinkInvalid => "Ce lien n'est pas valide. Demandez-en un nouveau au dispatcher.";

    public override string Dear(string name) => $"Bonjour {name},";
    public override string KindRegards => "Cordialement,";
    public override string PaymentConfirmed(string amount) => $"merci, votre paiement de {amount} est confirmé et votre trajet est réservé.";
    public override string Booking => "Réservation";
    public override string Pickup => "Prise en charge";
    public override string ItalianTime => "(heure italienne)";
    public override string From => "De";
    public override string To => "À";
    public override string Vehicle => "Véhicule";
    public override string VehicleSummary(VehicleCategory category, int passengers, int large, int small) =>
        $"{Category(category)}, {passengers} {(passengers == 1 ? "passager" : "passagers")}, {large} grands + {small} petits bagages";
    public override string Flight => "Vol";
    public override string MeetingPoint(string instructions) => $"Point de rendez-vous : {instructions}";
    public override string DriverDetailsFollow => "Le nom de votre chauffeur et le véhicule vous seront envoyés avant le trajet.";
    public override string ManageBooking => "Voir ou gérer votre réservation";
    public override string ViewBooking => "Voir votre réservation";
    public override string InvoiceAttached(string number) => $"Vous trouverez la facture {number} en pièce jointe.";
    public override string ConfirmedSubject(string reference) => $"Réservation confirmée – {reference}";
    public override string BookingCancelled(string reference) => $"votre réservation {reference} a été annulée.";
    public override string Refunded(string amount) =>
        $"Nous avons remboursé {amount} sur votre moyen de paiement d'origine. Selon votre banque, cela peut prendre 5 à 10 jours.";
    public override string CreditNoteAttached(string number) => $"Vous trouverez l'avoir {number} en pièce jointe.";
    public override string CancelledSubject(string reference) => $"Réservation annulée et remboursée – {reference}";
    public override string RefundSubject(string reference) => $"Remboursement – {reference}";
    public override string Transfer(string reference, DateTime local, string from, string to) =>
        $"Transfert {reference}, {local.ToString("dd/MM/yyyy HH:mm", Culture)} : {from} → {to}";
    public override string RefundForInvoice(string number) => $"Remboursement de la facture {number}";
    public override string DriverSubject(string reference) => $"Votre chauffeur pour {reference}";
    public override string YourDriver(string pickup, string where, string driver, string phone) =>
        $"votre chauffeur pour le {pickup} à {where} est {driver} ({phone}).";
    public override string Car(string car, string plate) => $"Véhicule : {car}, immatriculation {plate}.";
    public override string ReviewSubject(string reference) => $"Comment s'est passé votre trajet {reference} ?";
    public override string ReviewAsk => "merci d'avoir voyagé avec nous. Dites-nous comment se sont passés le trajet et le chauffeur : cela prend une minute.";
    public override string RateRide => "Évaluer le trajet";

    public override string Hello(string name) => $"Bonjour {name},";
    public override string ShiftCancelled(string when) => $"Votre service du {when} a été annulé.";
    public override string ShiftChanged(string when) => $"Votre service du {when} a été modifié. Veuillez le vérifier et le confirmer à nouveau.";
    public override string NewShift(string when, int rides, string plate) =>
        $"Nouveau service le {when} : {rides} {(rides == 1 ? "trajet" : "trajets")} avec {plate}. Veuillez le confirmer.";
    public override string OpenMyShifts => "Ouvrir mes services";
}
