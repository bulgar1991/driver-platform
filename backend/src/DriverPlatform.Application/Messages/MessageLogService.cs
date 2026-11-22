using System.ComponentModel.DataAnnotations;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Bookings;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Localization;
using DriverPlatform.Application.Notifications;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Messages;

/// <summary>Who a message was for.</summary>
public enum MessageAudience
{
    Any,
    Driver,
    Client,
}

public sealed record MessageQuery
{
    /// <summary>Office dates the message was sent, inclusive; at most 366 days.</summary>
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    [MaxLength(100)]
    public string? Search { get; init; }
    public NotificationChannel? Channel { get; init; }
    public NotificationStatus? Status { get; init; }
    public NotificationKind? Kind { get; init; }
    public MessageAudience Audience { get; init; }
    public Guid? BookingId { get; init; }
    public Guid? DriverId { get; init; }
    [Range(1, 10_000)]
    public int Page { get; init; } = 1;
    [Range(1, 200)]
    public int PageSize { get; init; } = 50;
}

/// <param name="SentLocal">Office time.</param>
/// <param name="CanResend">A shift, the driver details or a booking email can go out again; a review link cannot.</param>
public sealed record MessageItem(
    long Id,
    DateTime SentLocal,
    NotificationKind Kind,
    NotificationChannel Channel,
    NotificationStatus Status,
    string Recipient,
    string? Subject,
    string? Error,
    Guid? BookingId,
    string? BookingReference,
    string? ClientName,
    Guid? ClientId,
    Guid? DriverId,
    string? DriverName,
    Guid? AssignmentId,
    DateOnly? ShiftDate,
    bool CanResend);

public sealed record MessageCounts(int Total, int Sent, int Failed, int Skipped, int Email, int WhatsApp, int Sms);

public sealed record MessageList(IReadOnlyList<MessageItem> Items, int Total, int Page, int PageSize, MessageCounts Counts);

public interface IMessageLogService
{
    Task<MessageList> ListAsync(MessageQuery query, CancellationToken cancellationToken);
    /// <summary>Sends the message again, the way the panel would; the new attempt is a new line of the log.</summary>
    Task ResendAsync(long id, CancellationToken cancellationToken);
}

/// <summary>
/// Admin → Messages: every WhatsApp and email that went out to drivers and clients, with what failed
/// and why, and a way to send it again.
/// </summary>
public sealed class MessageLogService(
    IAppDbContext db,
    INotificationService notifications,
    IBookingEmails bookingEmails,
    IAuditLogger audit) : IMessageLogService
{
    public const int MaxDays = 366;

    private static readonly NotificationKind[] DriverKinds = [NotificationKind.DriverShift, NotificationKind.DriverShiftChanged];

    public static bool CanResend(NotificationKind kind, Guid? bookingId, Guid? assignmentId) => kind switch
    {
        NotificationKind.DriverShift or NotificationKind.DriverShiftChanged => assignmentId is not null,
        NotificationKind.ClientDriverDetails or NotificationKind.BookingConfirmation or NotificationKind.BookingRefund => bookingId is not null,
        _ => false,
    };

    public async Task<MessageList> ListAsync(MessageQuery query, CancellationToken cancellationToken)
    {
        if (query.To < query.From || query.To.DayNumber - query.From.DayNumber >= MaxDays)
        {
            throw new RequestValidationException("to", Texts.Current.RangeTooLong(MaxDays));
        }
        var start = OfficeTime.ToUtc(query.From.ToDateTime(TimeOnly.MinValue))!.Value;
        var end = OfficeTime.ToUtc(query.To.AddDays(1).ToDateTime(TimeOnly.MinValue))!.Value;

        var logs = db.NotificationLogs.AsNoTracking().Where(n => n.CreatedAt >= start && n.CreatedAt < end);
        if (query.Kind is { } kind)
        {
            logs = logs.Where(n => n.Kind == kind);
        }
        if (query.Audience == MessageAudience.Driver)
        {
            logs = logs.Where(n => DriverKinds.Contains(n.Kind));
        }
        else if (query.Audience == MessageAudience.Client)
        {
            logs = logs.Where(n => !DriverKinds.Contains(n.Kind));
        }
        if (query.BookingId is { } bookingId)
        {
            logs = logs.Where(n => n.BookingId == bookingId);
        }
        if (query.DriverId is { } driverId)
        {
            logs = logs.Where(n => n.DriverId == driverId);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            var digits = new string(term.Where(char.IsDigit).ToArray());
            logs = logs.Where(n =>
                n.Recipient.ToLower().Contains(term)
                || (digits.Length >= 4 && n.Recipient.Replace(" ", "").Contains(digits))
                || (n.Subject != null && n.Subject.ToLower().Contains(term))
                || db.Bookings.Any(b => b.Id == n.BookingId
                    && (b.Reference.ToLower().Contains(term) || (b.Client.FirstName + " " + b.Client.LastName).ToLower().Contains(term)))
                || db.Drivers.Any(d => d.Id == n.DriverId && (d.FirstName + " " + d.LastName).ToLower().Contains(term)));
        }

        // Counts before the channel and status filters, so the cards show what the chips would give.
        var counts = await logs
            .GroupBy(n => new { n.Channel, n.Status })
            .Select(g => new { g.Key.Channel, g.Key.Status, Count = g.Count() })
            .ToListAsync(cancellationToken);
        if (query.Channel is { } channel)
        {
            logs = logs.Where(n => n.Channel == channel);
        }
        if (query.Status is { } status)
        {
            logs = logs.Where(n => n.Status == status);
        }

        var total = await logs.CountAsync(cancellationToken);
        var page = await logs
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(n => new
            {
                Log = n,
                Booking = db.Bookings.Where(b => b.Id == n.BookingId)
                    .Select(b => new { b.Reference, b.ClientId, Name = b.Client.FirstName + " " + b.Client.LastName })
                    .FirstOrDefault(),
                Driver = db.Drivers.Where(d => d.Id == n.DriverId).Select(d => d.FirstName + " " + d.LastName).FirstOrDefault(),
                ShiftDate = db.Assignments.Where(a => a.Id == n.AssignmentId).Select(a => (DateOnly?)a.ShiftDate).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var items = page.Select(x => new MessageItem(
            x.Log.Id, OfficeTime.ToLocal(x.Log.CreatedAt), x.Log.Kind, x.Log.Channel, x.Log.Status, x.Log.Recipient,
            x.Log.Subject, x.Log.Error, x.Log.BookingId, x.Booking?.Reference, x.Booking?.Name.Trim(), x.Booking?.ClientId,
            x.Log.DriverId, x.Driver, x.Log.AssignmentId, x.ShiftDate,
            CanResend(x.Log.Kind, x.Log.BookingId, x.Log.AssignmentId))).ToList();

        var summary = new MessageCounts(
            counts.Sum(c => c.Count),
            counts.Where(c => c.Status == NotificationStatus.Sent).Sum(c => c.Count),
            counts.Where(c => c.Status == NotificationStatus.Failed).Sum(c => c.Count),
            counts.Where(c => c.Status == NotificationStatus.Skipped).Sum(c => c.Count),
            counts.Where(c => c.Channel == NotificationChannel.Email).Sum(c => c.Count),
            counts.Where(c => c.Channel == NotificationChannel.WhatsApp).Sum(c => c.Count),
            counts.Where(c => c.Channel == NotificationChannel.Sms).Sum(c => c.Count));
        return new MessageList(items, total, query.Page, query.PageSize, summary);
    }

    public async Task ResendAsync(long id, CancellationToken cancellationToken)
    {
        var log = await db.NotificationLogs.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, cancellationToken)
            ?? throw new NotFoundException(Texts.Current.MessageNotFound);
        if (!CanResend(log.Kind, log.BookingId, log.AssignmentId))
        {
            throw new RequestValidationException("id", Texts.Current.MessageCannotBeResent);
        }

        switch (log.Kind)
        {
            case NotificationKind.DriverShift or NotificationKind.DriverShiftChanged:
                var shift = await ShiftAsync(a => a.Id == log.AssignmentId, cancellationToken);
                await notifications.SendShiftToDriverAsync(shift, log.Kind, cancellationToken);
                break;

            case NotificationKind.ClientDriverDetails:
                var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == log.BookingId, cancellationToken)
                    ?? throw new NotFoundException(Texts.Current.BookingNotFound);
                if (booking.AssignmentId is null || booking.Status != BookingStatus.Assigned)
                {
                    throw new RequestValidationException("id", Texts.Current.MessageCannotBeResent);
                }
                // Forget what was sent, so this ride (and only this one) gets the message again.
                booking.DriverDetailsSentKey = null;
                await db.SaveChangesAsync(cancellationToken);
                await notifications.SendDriverDetailsAsync(await ShiftAsync(a => a.Id == booking.AssignmentId, cancellationToken), cancellationToken);
                break;

            default:
                var outbound = await db.Bookings.Include(b => b.Client).FirstOrDefaultAsync(b => b.Id == log.BookingId, cancellationToken)
                    ?? throw new NotFoundException(Texts.Current.BookingNotFound);
                var type = log.Kind == NotificationKind.BookingConfirmation ? InvoiceType.Invoice : InvoiceType.CreditNote;
                var invoice = await db.Invoices.Where(i => i.BookingId == outbound.Id && i.Type == type)
                    .OrderByDescending(i => i.CreatedAt).FirstOrDefaultAsync(cancellationToken)
                    ?? throw new RequestValidationException("id", Texts.Current.MessageCannotBeResent);
                if (type == InvoiceType.Invoice)
                {
                    await bookingEmails.SendConfirmationAsync(outbound, await db.LoadLegsAsync(outbound, cancellationToken), invoice, cancellationToken);
                }
                else
                {
                    await bookingEmails.SendCreditNoteAsync(outbound, invoice, outbound.Status == BookingStatus.Cancelled, cancellationToken);
                }
                break;
        }
        audit.Log("message.resent", nameof(NotificationLog), id.ToString(), new { log.Kind, log.Channel });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Assignment> ShiftAsync(System.Linq.Expressions.Expression<Func<Assignment, bool>> where, CancellationToken cancellationToken) =>
        await db.Assignments
            .Include(a => a.Driver)
            .Include(a => a.Vehicle)
            .Include(a => a.Bookings).ThenInclude(b => b.Client)
            .Include(a => a.Bookings).ThenInclude(b => b.Partner)
            .FirstOrDefaultAsync(where, cancellationToken)
        ?? throw new NotFoundException(Texts.Current.ShiftNotFound);
}
