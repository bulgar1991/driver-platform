using System.Globalization;
using System.Net;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Bookings;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.DriverPortal;
using DriverPlatform.Application.Geo;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DriverPlatform.Application.Notifications;

public interface INotificationService
{
    /// <summary>WhatsApp and email to the driver with the personal link. Needs Driver, Vehicle and Bookings loaded.</summary>
    Task SendShiftToDriverAsync(Assignment shift, NotificationKind kind, CancellationToken cancellationToken);

    /// <summary>Tells each client of the shift who drives them, once per driver and car.</summary>
    Task SendDriverDetailsAsync(Assignment shift, CancellationToken cancellationToken);

    Task SendReviewRequestAsync(Booking booking, string reviewUrl, NotificationChannel channel, CancellationToken cancellationToken);
}

/// <summary>
/// Business plan §5: messages to drivers and clients on WhatsApp and email. A message that fails
/// is logged (NotificationLog) and never breaks the action that triggered it.
/// </summary>
public sealed class NotificationService(
    IAppDbContext db,
    IWhatsAppSender whatsApp,
    IEmailSender email,
    IDriverLinks driverLinks,
    IBookingLinks bookingLinks,
    IOptions<CompanyOptions> company,
    TimeProvider clock,
    ILogger<NotificationService> logger) : INotificationService
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public async Task SendShiftToDriverAsync(Assignment shift, NotificationKind kind, CancellationToken cancellationToken)
    {
        var driver = shift.Driver;
        var url = driverLinks.Url(driver);
        var start = ItalyTime.ToLocal(shift.StartsAt);
        var when = start.ToString("ddd d MMM, HH:mm", Invariant);
        var rides = shift.Bookings.Count(b => b.Status != BookingStatus.Cancelled);
        var headline = kind switch
        {
            NotificationKind.DriverShiftChanged when shift.Status == AssignmentStatus.Cancelled => $"Your shift of {when} was cancelled.",
            NotificationKind.DriverShiftChanged => $"Your shift of {when} was changed. Please check and confirm it again.",
            _ => $"New shift on {when}: {rides} ride(s) with {shift.Vehicle.PlateNumber}. Please confirm it.",
        };

        await SendAsync(kind, NotificationChannel.WhatsApp, driver.Phone, log =>
        {
            log.DriverId = driver.Id;
            log.AssignmentId = shift.Id;
        }, () => whatsApp.SendTemplateAsync(driver.Phone, kind, "en", [driver.FirstName, headline, url], cancellationToken), cancellationToken);

        if (!string.IsNullOrEmpty(driver.Email))
        {
            var lines = shift.Bookings.Where(b => b.Status != BookingStatus.Cancelled).OrderBy(b => b.PickupAt)
                .Select(b => b.Describe()).ToList();
            await SendEmailAsync(kind, driver.Email, $"{driver.FirstName} {driver.LastName}", $"{headline}",
                [$"Hello {driver.FirstName},", headline, .. lines], ("Open my shifts", url),
                log => { log.DriverId = driver.Id; log.AssignmentId = shift.Id; }, cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SendDriverDetailsAsync(Assignment shift, CancellationToken cancellationToken)
    {
        var key = $"{shift.DriverId:N}:{shift.VehicleId:N}";
        var driver = shift.Driver;
        var vehicle = shift.Vehicle;
        var car = string.Join(" ", new[] { vehicle.Color, vehicle.Make, vehicle.Model }.Where(s => !string.IsNullOrEmpty(s)));

        var silent = await SilentPartnersAsync(shift.Bookings, cancellationToken);
        foreach (var booking in shift.Bookings.Where(b => b.Status == BookingStatus.Assigned && b.DriverDetailsSentKey != key))
        {
            if (booking.PartnerId is { } partnerId && silent.Contains(partnerId))
            {
                // The partner talks to its own passengers.
                continue;
            }
            var client = booking.Client;
            var pickup = ItalyTime.ToLocal(booking.PickupAt).ToString("ddd d MMM, HH:mm", Invariant);
            var where = booking.PickupMeetingPoint?.Name ?? booking.Pickup.Address;
            var details = $"{driver.FirstName}, {driver.Phone}, {car} ({vehicle.PlateNumber})";
            var manage = bookingLinks.ManageUrl(booking.ReturnOfBookingId is null ? booking : await OutboundAsync(booking, cancellationToken));

            await SendAsync(NotificationKind.ClientDriverDetails, NotificationChannel.WhatsApp, client.Phone, log => log.BookingId = booking.Id,
                () => whatsApp.SendTemplateAsync(client.Phone, NotificationKind.ClientDriverDetails, client.Language,
                    [client.FirstName, booking.Reference, pickup, where, details], cancellationToken), cancellationToken);
            if (!string.IsNullOrEmpty(client.Email))
            {
                await SendEmailAsync(NotificationKind.ClientDriverDetails, client.Email, $"{client.FirstName} {client.LastName}",
                    $"Your driver for {booking.Reference}",
                    [
                        $"Dear {client.FirstName},",
                        $"your driver for {pickup} at {where} is {driver.FirstName} ({driver.Phone}).",
                        $"Car: {car}, plate {vehicle.PlateNumber}.",
                        booking.PickupMeetingPoint?.ClientInstructions ?? "",
                    ],
                    ("View your booking", manage),
                    log => log.BookingId = booking.Id, cancellationToken);
            }
            booking.DriverDetailsSentKey = key;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SendReviewRequestAsync(Booking booking, string reviewUrl, NotificationChannel channel, CancellationToken cancellationToken)
    {
        var client = booking.Client;
        if (channel == NotificationChannel.Email && !string.IsNullOrEmpty(client.Email))
        {
            await SendEmailAsync(NotificationKind.ReviewRequest, client.Email, $"{client.FirstName} {client.LastName}",
                $"How was your ride {booking.Reference}?",
                [$"Dear {client.FirstName},", "thank you for travelling with us. Tell us how the ride and the driver were — it takes a minute."],
                ("Rate your ride", reviewUrl),
                log => log.BookingId = booking.Id, cancellationToken);
        }
        else
        {
            await SendAsync(NotificationKind.ReviewRequest, NotificationChannel.WhatsApp, client.Phone, log => log.BookingId = booking.Id,
                () => whatsApp.SendTemplateAsync(client.Phone, NotificationKind.ReviewRequest, client.Language,
                    [client.FirstName, booking.Reference, reviewUrl], cancellationToken), cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Partners that asked us not to message their passengers.</summary>
    private async Task<HashSet<Guid>> SilentPartnersAsync(IEnumerable<Booking> bookings, CancellationToken cancellationToken)
    {
        var ids = bookings.Where(b => b.PartnerId != null).Select(b => b.PartnerId!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }
        return (await db.Partners.Where(p => ids.Contains(p.Id) && !p.NotifyPassengers).Select(p => p.Id).ToListAsync(cancellationToken)).ToHashSet();
    }

    private async Task<Booking> OutboundAsync(Booking returnLeg, CancellationToken cancellationToken) =>
        await db.Bookings.FindAsync([returnLeg.ReturnOfBookingId!.Value], cancellationToken) ?? returnLeg;

    private Task SendEmailAsync(
        NotificationKind kind,
        string to,
        string toName,
        string subject,
        IReadOnlyList<string> paragraphs,
        (string Label, string Url) button,
        Action<NotificationLog> link,
        CancellationToken cancellationToken)
    {
        var lines = paragraphs.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        var html =
            "<div style=\"font-family:Arial,Helvetica,sans-serif;color:#1a1a1a;font-size:14px;line-height:1.5\">" +
            $"<p style=\"font-size:18px;letter-spacing:2px\">{Encode(company.Value.Name.ToUpperInvariant())}</p>" +
            string.Concat(lines.Select(l => $"<p>{Encode(l)}</p>")) +
            $"<p><a href=\"{Encode(button.Url)}\" style=\"display:inline-block;background:#C9A45C;color:#0B0B0C;padding:10px 18px;text-decoration:none\">{Encode(button.Label)}</a></p>" +
            "</div>";
        var text = string.Join("\n\n", lines) + $"\n\n{button.Label}: {button.Url}\n";

        return SendAsync(kind, NotificationChannel.Email, to, link, async () =>
        {
            await email.SendAsync(new EmailMessage(to, toName, subject, html, text, []), cancellationToken);
            return null;
        }, cancellationToken);
    }

    private async Task SendAsync(
        NotificationKind kind,
        NotificationChannel channel,
        string recipient,
        Action<NotificationLog> link,
        Func<Task<string?>> send,
        CancellationToken cancellationToken)
    {
        var log = new NotificationLog
        {
            Kind = kind,
            Channel = channel,
            Recipient = recipient,
            CreatedAt = clock.GetUtcNow(),
        };
        link(log);
        if (string.IsNullOrWhiteSpace(recipient))
        {
            log.Status = NotificationStatus.Skipped;
        }
        else
        {
            try
            {
                log.ProviderMessageId = await send();
                log.Status = NotificationStatus.Sent;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.Status = NotificationStatus.Failed;
                log.Error = ex.Message.Length > 480 ? ex.Message[..480] : ex.Message;
                logger.LogError(ex, "{Kind} by {Channel} to {Recipient} failed", kind, channel, recipient);
            }
        }
        db.NotificationLogs.Add(log);
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
