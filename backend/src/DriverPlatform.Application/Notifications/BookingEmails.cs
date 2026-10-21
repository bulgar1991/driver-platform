using System.Globalization;
using System.Net;
using System.Text;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Bookings;
using DriverPlatform.Application.Common;
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

/// <summary>Client emails in English for now; translations come with the multilingual site.</summary>
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
        var html = new StringBuilder();
        var text = new StringBuilder();
        Paragraph(html, text, $"Dear {outbound.Client.FirstName},");
        Paragraph(html, text, $"thank you, your payment of {Money(invoice.TotalAmount)} is confirmed and your ride is booked.");

        foreach (var leg in legs)
        {
            var summary = leg.ToLegSummary();
            html.Append("<table style=\"border-collapse:collapse;margin:16px 0;width:100%;max-width:520px\">");
            Row(html, "Booking", leg.Reference);
            Row(html, "Pickup", summary.PickupAt.ToString("dddd d MMMM yyyy, HH:mm", CultureInfo.InvariantCulture) + " (Italian time)");
            Row(html, "From", leg.PickupMeetingPoint is { } pm ? $"{pm.Name} – {pm.Address}" : leg.Pickup.Address);
            Row(html, "To", leg.DropoffMeetingPoint is { } dm ? $"{dm.Name} – {dm.Address}" : leg.Dropoff.Address);
            Row(html, "Vehicle", $"{leg.VehicleCategory}, {leg.Passengers} passengers, {leg.LargeLuggage} large + {leg.SmallLuggage} small bags");
            if (leg.FlightNumber is not null)
            {
                Row(html, "Flight", leg.FlightNumber);
            }
            html.Append("</table>");

            text.AppendLine(leg.Describe());
            if (leg.PickupMeetingPoint?.ClientInstructions is { } instructions)
            {
                Paragraph(html, text, $"Meeting point: {instructions}");
            }
        }

        Paragraph(html, text, "Your driver's name and car will be sent to you before the ride.");
        Link(html, text, "View or manage your booking", links.ManageUrl(outbound));
        Paragraph(html, text, $"The invoice {invoice.Number} is attached.");
        Signature(html, text);

        await SendAsync(
            outbound, invoice,
            $"Booking confirmed – {outbound.Reference}",
            html, text, cancellationToken);
    }

    public async Task SendCreditNoteAsync(Booking outbound, Invoice creditNote, bool cancelled, CancellationToken cancellationToken)
    {
        var html = new StringBuilder();
        var text = new StringBuilder();
        Paragraph(html, text, $"Dear {outbound.Client.FirstName},");
        if (cancelled)
        {
            Paragraph(html, text, $"your booking {outbound.Reference} has been cancelled.");
        }
        Paragraph(html, text,
            $"We have refunded {Money(creditNote.TotalAmount)} to your original payment method. " +
            "Depending on your bank it can take 5–10 days to appear.");
        Paragraph(html, text, $"The credit note {creditNote.Number} is attached.");
        Link(html, text, "View your booking", links.ManageUrl(outbound));
        Signature(html, text);

        await SendAsync(
            outbound, creditNote,
            cancelled ? $"Booking cancelled and refunded – {outbound.Reference}" : $"Refund – {outbound.Reference}",
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

    private void Signature(StringBuilder html, StringBuilder text)
    {
        var c = company.Value;
        var contact = string.Join(" · ", new[] { c.Phone, c.Email }.Where(s => !string.IsNullOrWhiteSpace(s)));
        Paragraph(html, text, $"Kind regards,\n{c.Name}" + (contact.Length > 0 ? $"\n{contact}" : ""));
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

    private static string Money(decimal amount) => amount.ToString("€#,##0.00", CultureInfo.InvariantCulture);

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
