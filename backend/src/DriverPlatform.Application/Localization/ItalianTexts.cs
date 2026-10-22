using System.Globalization;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Localization;

/// <summary>Formal "Lei", as on the site.</summary>
internal sealed class ItalianTexts : Texts
{
    public override string Language => "it";
    public override CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("it-IT");

    public override string Category(VehicleCategory category) => category switch
    {
        VehicleCategory.Sedan => "Berlina",
        VehicleCategory.Estate => "Station wagon / SUV",
        _ => "Van",
    };
    public override string ReturnDiscount(decimal percent) => $"Viaggio di ritorno −{percent.ToString("0.##", Culture)}%";
    public override string ZoneAccess(string zone) => $"Accesso {zone}";
    public override string ChildSeat => "Seggiolino";
    public override string ExtraLuggage => "Bagaglio extra";
    public override string NameSign => "Cartello con nome";
    public override string PublicHolidays => "Giorni festivi";
    public override string UpToPassengers(int count) => $"Fino a {count} passeggeri";
    public override string UpToLargeBags(int count) => $"Fino a {count} bagagli grandi";

    public override string AcceptPrivacyToBook => "Per prenotare accetti l'informativa sulla privacy.";
    public override string AcceptPrivacy => "Accetti l'informativa sulla privacy.";
    public override string CategoryNotOffered => "Questa categoria di veicolo non è disponibile.";
    public override string VehicleDoesNotFit => "Questo veicolo non è adatto al viaggio.";
    public override string PriceChanged => "Il prezzo è cambiato. Controlli il nuovo prezzo e confermi di nuovo.";
    public override string BookingWasCancelled => "Questa prenotazione è stata annullata.";
    public override string BookingAlreadyPaid => "Questa prenotazione è già pagata.";
    public override string PayTheOutbound => "Paghi la prenotazione di andata: il ritorno è incluso.";
    public override string ReturnAfterArrival => "Il ritorno deve partire dopo l'arrivo della prima corsa.";
    public override string ChildSeatLimit => "Al massimo un seggiolino per passeggero e tre per auto.";
    public override string ChildSeatGroupOnce => "Indichi ogni gruppo di seggiolino una sola volta.";
    public override string OutsideServiceArea => "Operiamo in Italia, Francia, Germania e Svizzera.";
    public override string TooClose => "Partenza e arrivo sono troppo vicini.";
    public override string TimeDoesNotExist => "Quest'ora non esiste (passaggio all'ora legale). Scelga un altro orario.";
    public override string BookAhead(int hours) => $"Prenoti con almeno {hours} ore di anticipo.";
    public override string OneYearAhead => "Si può prenotare fino a un anno prima.";
    public override string NoMeetingPointYet(string zone) => $"{zone} non ha ancora un punto d'incontro. La preghiamo di contattarci.";
    public override string NotAMeetingPoint(string requested, string zone) => $"\"{requested}\" non è un punto d'incontro di {zone}.";
    public override string BookingNotFound => "Prenotazione non trovata.";
    public override string InvoiceNotFound => "Fattura non trovata.";
    public override string AlreadyRated => "Grazie, questa corsa è già stata valutata.";
    public override string ReviewLinkInvalid => "Questo link per la recensione non è valido o è scaduto.";

    public override string ShiftNotFound => "Turno non trovato.";
    public override string ShiftAlreadyConfirmed => "Già confermato.";
    public override string ShiftCannotBeConfirmed => "Questo turno non può più essere confermato.";
    public override string RideNotFound => "Corsa non trovata.";
    public override string ConfirmShiftFirst => "Confermi prima il turno.";
    public override string NotWaitingForPickup => "Questa corsa non è in attesa di presa a bordo.";
    public override string MarkPickupFirst => "Segni prima la presa a bordo.";
    public override string PickupFrom(int hours) => $"Può segnare la presa a bordo da {hours} ore prima dell'orario previsto.";
    public override string DriverLinkInvalid => "Questo link non è valido. Ne chieda uno nuovo al dispatcher.";

    public override string Dear(string name) => $"Gentile {name},";
    public override string KindRegards => "Cordiali saluti,";
    public override string PaymentConfirmed(string amount) => $"grazie, il pagamento di {amount} è confermato e la corsa è prenotata.";
    public override string Booking => "Prenotazione";
    public override string Pickup => "Partenza";
    public override string ItalianTime => "(ora italiana)";
    public override string From => "Da";
    public override string To => "A";
    public override string Vehicle => "Veicolo";
    public override string VehicleSummary(VehicleCategory category, int passengers, int large, int small) =>
        $"{Category(category)}, {passengers} {(passengers == 1 ? "passeggero" : "passeggeri")}, {large} bagagli grandi + {small} piccoli";
    public override string Flight => "Volo";
    public override string MeetingPoint(string instructions) => $"Punto d'incontro: {instructions}";
    public override string DriverDetailsFollow => "Il nome dell'autista e l'auto Le saranno inviati prima della corsa.";
    public override string ManageBooking => "Vedi o gestisci la prenotazione";
    public override string ViewBooking => "Vedi la prenotazione";
    public override string InvoiceAttached(string number) => $"In allegato la fattura {number}.";
    public override string ConfirmedSubject(string reference) => $"Prenotazione confermata – {reference}";
    public override string BookingCancelled(string reference) => $"la Sua prenotazione {reference} è stata annullata.";
    public override string Refunded(string amount) =>
        $"Le abbiamo rimborsato {amount} sul metodo di pagamento originale. A seconda della banca possono servire 5–10 giorni.";
    public override string CreditNoteAttached(string number) => $"In allegato la nota di credito {number}.";
    public override string CancelledSubject(string reference) => $"Prenotazione annullata e rimborsata – {reference}";
    public override string RefundSubject(string reference) => $"Rimborso – {reference}";
    public override string Transfer(string reference, DateTime local, string from, string to) =>
        $"Transfer {reference}, {local.ToString("dd/MM/yyyy HH:mm", Culture)}: {from} → {to}";
    public override string RefundForInvoice(string number) => $"Rimborso della fattura {number}";
    public override string DriverSubject(string reference) => $"Il Suo autista per {reference}";
    public override string YourDriver(string pickup, string where, string driver, string phone) =>
        $"il Suo autista per {pickup} a {where} è {driver} ({phone}).";
    public override string Car(string car, string plate) => $"Auto: {car}, targa {plate}.";
    public override string ReviewSubject(string reference) => $"Com'è andata la corsa {reference}?";
    public override string ReviewAsk => "grazie per aver viaggiato con noi. Ci dica com'erano la corsa e l'autista: ci vuole un minuto.";
    public override string RateRide => "Valuti la corsa";

    public override string Hello(string name) => $"Buongiorno {name},";
    public override string ShiftCancelled(string when) => $"Il Suo turno di {when} è stato annullato.";
    public override string ShiftChanged(string when) => $"Il Suo turno di {when} è cambiato. Lo controlli e lo confermi di nuovo.";
    public override string NewShift(string when, int rides, string plate) =>
        $"Nuovo turno {when}: {rides} {(rides == 1 ? "corsa" : "corse")} con {plate}. La preghiamo di confermarlo.";
    public override string OpenMyShifts => "Apra i miei turni";
}
