using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Reviews;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using DriverPlatform.Application.Localization;

namespace DriverPlatform.Application.Dispatch;

public interface IBookingAdminService
{
    Task<PagedResult<BookingListItem>> ListAsync(BookingQuery query, CancellationToken cancellationToken);
    Task<BookingDetail> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<BookingDetail> ChangeStatusAsync(Guid id, BookingStatusRequest request, CancellationToken cancellationToken);
    Task<BookingDetail> UpdateNotesAsync(Guid id, BookingNotesRequest request, CancellationToken cancellationToken);
}

public sealed class BookingAdminService(IAppDbContext db, IReviewService reviews, IAuditLogger audit, TimeProvider clock) : IBookingAdminService
{
    /// <summary>What the dispatcher may set by hand; the driver link sets the same statuses in step 5.</summary>
    private static readonly Dictionary<BookingStatus, BookingStatus[]> Allowed = new()
    {
        [BookingStatus.AwaitingPayment] = [BookingStatus.Cancelled],
        [BookingStatus.New] = [BookingStatus.Cancelled],
        [BookingStatus.Assigned] = [BookingStatus.PickedUp, BookingStatus.Completed, BookingStatus.Cancelled],
        [BookingStatus.PickedUp] = [BookingStatus.Completed],
        [BookingStatus.Completed] = [],
        [BookingStatus.Cancelled] = [],
    };

    public async Task<PagedResult<BookingListItem>> ListAsync(BookingQuery query, CancellationToken cancellationToken)
    {
        var bookings = Query();
        if (query.From is { } from)
        {
            var start = ItalyTime.ToUtc(from.ToDateTime(TimeOnly.MinValue))!.Value;
            bookings = bookings.Where(b => b.PickupAt >= start);
        }
        if (query.To is { } to)
        {
            var end = ItalyTime.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue))!.Value;
            bookings = bookings.Where(b => b.PickupAt < end);
        }
        if (query.Status is { } status)
        {
            bookings = bookings.Where(b => b.Status == status);
        }
        else if (!query.IncludeUnpaid)
        {
            bookings = bookings.Where(b => b.Status != BookingStatus.AwaitingPayment);
        }
        if (query.Unassigned)
        {
            bookings = bookings.Where(b => b.Status == BookingStatus.New && b.AssignmentId == null);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            bookings = bookings.Where(b =>
                b.Reference.ToLower().Contains(term)
                || b.Client.LastName.ToLower().Contains(term)
                || b.Client.FirstName.ToLower().Contains(term)
                || b.Client.Phone.Contains(term)
                || (b.Client.Email != null && b.Client.Email.Contains(term))
                || (b.FlightNumber != null && b.FlightNumber.ToLower().Contains(term))
                || (b.ExternalReference != null && b.ExternalReference.ToLower().Contains(term)));
        }

        var total = await bookings.CountAsync(cancellationToken);
        var page = await bookings
            .OrderBy(b => b.PickupAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<BookingListItem>(page.Select(b => b.ToListItem()).ToList(), total, query.Page, query.PageSize);
    }

    public async Task<BookingDetail> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await ToDetailAsync(await FindAsync(id, cancellationToken), cancellationToken);

    public async Task<BookingDetail> ChangeStatusAsync(Guid id, BookingStatusRequest request, CancellationToken cancellationToken)
    {
        var booking = await FindAsync(id, cancellationToken);
        if (!Allowed[booking.Status].Contains(request.Status))
        {
            throw new RequestValidationException("status", Texts.Current.BookingStatusChange(booking.Status, request.Status));
        }
        if (request.Status == BookingStatus.Cancelled && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new RequestValidationException("reason", Texts.Current.SayWhyBookingCancelled);
        }

        var previous = booking.Status;
        var now = clock.GetUtcNow();
        booking.Status = request.Status;
        switch (request.Status)
        {
            case BookingStatus.PickedUp:
                booking.PickedUpAt = now;
                break;
            case BookingStatus.Completed:
                booking.PickedUpAt ??= now;
                booking.CompletedAt = now;
                break;
            case BookingStatus.Cancelled:
                booking.CancelledAt = now;
                booking.CancellationReason = request.Reason!.Trim();
                break;
        }

        if (booking.Assignment is not null)
        {
            await ShiftService.SyncAsync(db, booking.Assignment, now, cancellationToken);
        }

        audit.Log("booking.status_changed", nameof(Booking), booking.Id.ToString(),
            new { booking.Reference, From = previous, To = request.Status, request.Reason });
        await db.SaveChangesAsync(cancellationToken);

        if (request.Status == BookingStatus.Completed)
        {
            await reviews.RequestAsync(booking, cancellationToken);
        }
        return await ToDetailAsync(booking, cancellationToken);
    }

    public async Task<BookingDetail> UpdateNotesAsync(Guid id, BookingNotesRequest request, CancellationToken cancellationToken)
    {
        var booking = await FindAsync(id, cancellationToken);
        booking.InternalNotes = string.IsNullOrWhiteSpace(request.InternalNotes) ? null : request.InternalNotes.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return await ToDetailAsync(booking, cancellationToken);
    }

    private IQueryable<Booking> Query() => db.Bookings
        .Include(b => b.Client)
        .Include(b => b.Partner)
        .Include(b => b.Assignment).ThenInclude(a => a!.Driver)
        .Include(b => b.Assignment).ThenInclude(a => a!.Vehicle);

    private async Task<Booking> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await Query().FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
        ?? throw new NotFoundException(Texts.Current.BookingNotFound);

    private async Task<BookingDetail> ToDetailAsync(Booking b, CancellationToken cancellationToken)
    {
        var paymentOwner = b.ReturnOfBookingId ?? b.Id;
        var payment = await db.Payments.AsNoTracking()
            .Where(p => p.BookingId == paymentOwner)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => (PaymentStatus?)p.Status)
            .FirstOrDefaultAsync(cancellationToken);
        var linked = b.ReturnOfBookingId is { } outboundId
            ? await db.Bookings.Where(x => x.Id == outboundId).Select(x => x.Reference).FirstOrDefaultAsync(cancellationToken)
            : await db.Bookings.Where(x => x.ReturnOfBookingId == b.Id).Select(x => x.Reference).FirstOrDefaultAsync(cancellationToken);
        var item = b.ToListItem();

        return new BookingDetail(
            b.Id, b.Reference, b.Status, b.Source, item.PickupAt, b.Pickup, b.Dropoff,
            b.PickupMeetingPoint, b.DropoffMeetingPoint, b.Passengers, b.LargeLuggage, b.SmallLuggage, b.ChildSeats,
            b.FlightNumber, b.NameSign, b.NameSignText, b.ClientNotes, b.InternalNotes, b.VehicleCategory,
            b.DistanceKm, b.DurationMinutes, b.TotalPrice, b.Currency, b.PriceLines,
            new BookingClient(item.ClientName, b.Client.Email, b.Client.Phone, b.Client.Language),
            payment, b.AssignmentId, item.DriverName, item.VehiclePlate, linked, b.CancellationReason, b.CreatedAt,
            b.Partner?.Name, b.ExternalReference);
    }
}
