using System.Net;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Bookings;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.DriverPortal;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Localization;
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
    public async Task SendShiftToDriverAsync(Assignment shift, NotificationKind kind, CancellationToken cancellationToken)
    {
        var driver = shift.Driver;
        var t = DriverTexts(driver);
        var url = driverLinks.Url(driver);
        var when = t.ShortDateTime(ItalyTime.ToLocal(shift.StartsAt));
        var rides = shift.Bookings.Count(b => b.Status != BookingStatus.Cancelled);
        var headline = kind switch
        {
            NotificationKind.DriverShiftChanged when shift.Status == AssignmentStatus.Cancelled => t.ShiftCancelled(when),
            NotificationKind.DriverShiftChanged => t.ShiftChanged(when),
            _ => t.NewShift(when, rides, shift.Vehicle.PlateNumber),
        };

        await SendAsync(kind, NotificationChannel.WhatsApp, driver.Phone, log =>
        {
            log.DriverId = driver.Id;
            log.AssignmentId = shift.Id;
        }, () => whatsApp.SendTemplateAsync(driver.Phone, kind, t.Language, [driver.FirstName, headline, url], cancellationToken), cancellationToken);

        if (!string.IsNullOrEmpty(driver.Email))
        {
            var lines = shift.Bookings.Where(b => b.Status != BookingStatus.Cancelled).OrderBy(b => b.PickupAt)
                .Select(b => b.Describe(t)).ToList();
            await SendEmailAsync(kind, driver.Email, $"{driver.FirstName} {driver.LastName}", headline,
                [t.Hello(driver.FirstName), headline, .. lines], (t.OpenMyShifts, url),
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
            var t = Texts.For(client.Language);
            var pickup = t.ShortDateTime(ItalyTime.ToLocal(booking.PickupAt));
            var where = booking.PickupMeetingPoint?.Name ?? booking.Pickup.Address;
            var details = $"{driver.FirstName}, {driver.Phone}, {car} ({vehicle.PlateNumber})";
            var manage = bookingLinks.ManageUrl(booking.ReturnOfBookingId is null ? booking : await OutboundAsync(booking, cancellationToken));

            await SendAsync(NotificationKind.ClientDriverDetails, NotificationChannel.WhatsApp, client.Phone, log => log.BookingId = booking.Id,
                () => whatsApp.SendTemplateAsync(client.Phone, NotificationKind.ClientDriverDetails, client.Language,
                    [client.FirstName, booking.Reference, pickup, where, details], cancellationToken), cancellationToken);
            if (!string.IsNullOrEmpty(client.Email))
            {
                await SendEmailAsync(NotificationKind.ClientDriverDetails, client.Email, $"{client.FirstName} {client.LastName}",
                    t.DriverSubject(booking.Reference),
                    [
                        t.Dear(client.FirstName),
                        t.YourDriver(pickup, where, driver.FirstName, driver.Phone),
                        t.Car(car, vehicle.PlateNumber),
                        booking.PickupMeetingPoint?.Instructions(t) ?? "",
                    ],
                    (t.ViewBooking, manage),
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
            var t = Texts.For(client.Language);
            await SendEmailAsync(NotificationKind.ReviewRequest, client.Email, $"{client.FirstName} {client.LastName}",
                t.ReviewSubject(booking.Reference),
                [t.Dear(client.FirstName), t.ReviewAsk],
                (t.RateRide, reviewUrl),
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

    /// <summary>The driver's first language the site has, else Italian: drivers work in Italy.</summary>
    private static Texts DriverTexts(Driver driver) =>
        driver.Languages.Select(l => l.Trim().ToLowerInvariant()).FirstOrDefault(l => l is "it" or "en" or "de" or "fr") is { } language
            ? Texts.For(language)
            : Texts.Italian;

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
