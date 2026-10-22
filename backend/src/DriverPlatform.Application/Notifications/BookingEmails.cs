using System.Net;
using System.Text;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Bookings;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Localization;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DriverPlatform.Application.Notifications;

public interface IBookingEmails
{
    /// <summary>Booking confirmation with the invoice attached. Failures are logged, never thrown: the payment stands.</summary>
    Task SendConfirmationAsync(Booking outbound, IReadOnlyList<Booking> legs, Invoice invoice, CancellationToken cancellationToken);

    Task SendCreditNoteAsync(Booking outbound, Invoice creditNote, bool cancelled, CancellationToken cancellationToken);
}

/// <summary>Client emails, in the language the client booked in.</summary>
public sealed class BookingEmails(
    IEmailSender sender,
    IAppDbContext db,
    IBookingLinks links,
    IOptions<CompanyOptions> company,
    TimeProvider clock,
    ILogger<BookingEmails> logger) : IBookingEmails
{
    public async Task SendConfirmationAsync(
        Booking outbound, IReadOnlyList<Booking> legs, Invoice invoice, CancellationToken cancellationToken)
    {
        var t = Texts.For(outbound.Client.Language);
        var html = new StringBuilder();
        var text = new StringBuilder();
        Paragraph(html, text, t.Dear(outbound.Client.FirstName));
        Paragraph(html, text, t.PaymentConfirmed(t.Money(invoice.TotalAmount)));

        foreach (var leg in legs)
        {
            var summary = leg.ToLegSummary();
            html.Append("<table style=\"border-collapse:collapse;margin:16px 0;width:100%;max-width:520px\">");
            Row(html, t.Booking, leg.Reference);
            Row(html, t.Pickup, $"{t.LongDateTime(summary.PickupAt)} {t.ItalianTime}");
            Row(html, t.From, leg.PickupMeetingPoint is { } pm ? $"{pm.Name} – {pm.Address}" : leg.Pickup.Address);
            Row(html, t.To, leg.DropoffMeetingPoint is { } dm ? $"{dm.Name} – {dm.Address}" : leg.Dropoff.Address);
            Row(html, t.Vehicle, t.VehicleSummary(leg.VehicleCategory, leg.Passengers, leg.LargeLuggage, leg.SmallLuggage));
            if (leg.FlightNumber is not null)
            {
                Row(html, t.Flight, leg.FlightNumber);
            }
            html.Append("</table>");

            text.AppendLine(leg.Describe(t));
            if (leg.PickupMeetingPoint?.Instructions(t) is { } instructions)
            {
                Paragraph(html, text, t.MeetingPoint(instructions));
            }
        }

        Paragraph(html, text, t.DriverDetailsFollow);
        Link(html, text, t.ManageBooking, links.ManageUrl(outbound));
        Paragraph(html, text, t.InvoiceAttached(invoice.Number));
        Signature(html, text, t);

        await SendAsync(outbound, invoice, t.ConfirmedSubject(outbound.Reference), html, text, cancellationToken);
    }

    public async Task SendCreditNoteAsync(Booking outbound, Invoice creditNote, bool cancelled, CancellationToken cancellationToken)
    {
        var t = Texts.For(outbound.Client.Language);
        var html = new StringBuilder();
        var text = new StringBuilder();
        Paragraph(html, text, t.Dear(outbound.Client.FirstName));
        if (cancelled)
        {
            Paragraph(html, text, t.BookingCancelled(outbound.Reference));
        }
        Paragraph(html, text, t.Refunded(t.Money(creditNote.TotalAmount)));
        Paragraph(html, text, t.CreditNoteAttached(creditNote.Number));
        Link(html, text, t.ViewBooking, links.ManageUrl(outbound));
        Signature(html, text, t);

        await SendAsync(
            outbound, creditNote,
            cancelled ? t.CancelledSubject(outbound.Reference) : t.RefundSubject(outbound.Reference),
            html, text, cancellationToken);
    }

    private async Task SendAsync(
        Booking outbound, Invoice document, string subject, StringBuilder html, StringBuilder text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(outbound.Client.Email))
        {
            return;
        }

        var label = document.Type == InvoiceType.Invoice ? "Invoice" : "CreditNote";
        var attachments = document.Pdf is null
            ? []
            : new[] { new EmailAttachment($"{label}-{document.Number}.pdf", "application/pdf", document.Pdf) };
        var message = new EmailMessage(
            outbound.Client.Email,
            $"{outbound.Client.FirstName} {outbound.Client.LastName}".Trim(),
            subject,
            Layout(html.ToString()),
            text.ToString(),
            attachments);

        try
        {
            await sender.SendAsync(message, cancellationToken);
            document.Status = InvoiceStatus.Sent;
            document.EmailSentAt = clock.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The invoice stays "Issued"; it can be resent from the panel.
            logger.LogError(ex, "Could not email {Number} for {Reference}", document.Number, outbound.Reference);
        }
    }

    private string Layout(string body) =>
        "<div style=\"font-family:Arial,Helvetica,sans-serif;color:#1a1a1a;font-size:14px;line-height:1.5\">" +
        $"<p style=\"font-size:18px;letter-spacing:2px;color:#0B0B0C\">{Encode(company.Value.Name.ToUpperInvariant())}</p>" +
        body + "</div>";

    private void Signature(StringBuilder html, StringBuilder text, Texts t)
    {
        var c = company.Value;
        var contact = string.Join(" · ", new[] { c.Phone, c.Email }.Where(s => !string.IsNullOrWhiteSpace(s)));
        Paragraph(html, text, $"{t.KindRegards}\n{c.Name}" + (contact.Length > 0 ? $"\n{contact}" : ""));
    }

    private static void Paragraph(StringBuilder html, StringBuilder text, string value)
    {
        html.Append("<p>").Append(Encode(value).Replace("\n", "<br>")).Append("</p>");
        text.AppendLine(value).AppendLine();
    }

    private static void Row(StringBuilder html, string label, string value) =>
        html.Append("<tr><td style=\"padding:4px 12px 4px 0;color:#777;vertical-align:top\">")
            .Append(Encode(label))
            .Append("</td><td style=\"padding:4px 0\">")
            .Append(Encode(value))
            .Append("</td></tr>");

    private static void Link(StringBuilder html, StringBuilder text, string label, string url)
    {
        html.Append($"<p><a href=\"{Encode(url)}\" style=\"display:inline-block;background:#C9A45C;color:#0B0B0C;padding:10px 18px;text-decoration:none;border-radius:2px\">{Encode(label)}</a></p>");
        text.AppendLine($"{label}: {url}").AppendLine();
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
