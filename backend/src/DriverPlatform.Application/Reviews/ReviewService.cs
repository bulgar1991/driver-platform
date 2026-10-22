using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Notifications;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DriverPlatform.Application.Reviews;

public sealed record ReviewPage(string Reference, DateTime PickupAt, string From, string To, string DriverFirstName, bool Submitted);

public sealed record SubmitReviewRequest
{
    [Range(1, 5)]
    public int Rating { get; init; }
    [MaxLength(2000)]
    public string? Comment { get; init; }
    /// <summary>Opt-in: show this review on the website with the first name and last initial.</summary>
    public bool PublishConsent { get; init; }
}

/// <summary>A published review: first name and last initial only, never contact details or the route.</summary>
public sealed record Testimonial(string Author, int Rating, string Comment, DateOnly Date, string VehicleCategory);

public sealed record ReviewDto(
    Guid Id,
    string BookingReference,
    DateTime PickupAt,
    Guid DriverId,
    string DriverName,
    string ClientName,
    int? Rating,
    string? Comment,
    NotificationChannel Channel,
    DateTimeOffset SentAt,
    DateTimeOffset? SubmittedAt);

public interface IReviewService
{
    /// <summary>After drop-off: creates the one-time review link and sends it. Needs Client and Assignment loaded.</summary>
    Task RequestAsync(Booking booking, CancellationToken cancellationToken);
    Task<ReviewPage> GetAsync(string token, CancellationToken cancellationToken);
    Task SubmitAsync(string token, SubmitReviewRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReviewDto>> ListAsync(Guid? driverId, int take, CancellationToken cancellationToken);
    /// <summary>Good reviews whose authors agreed to publication, newest first.</summary>
    Task<IReadOnlyList<Testimonial>> TestimonialsAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Business plan §5 step 7: after "completed" the client gets a link by email (or WhatsApp when there
/// is no email) to rate the ride and the driver. The link works once and expires after 30 days.
/// </summary>
public sealed class ReviewService(
    IAppDbContext db,
    INotificationService notifications,
    IOptions<PaymentsOptions> site,
    TimeProvider clock) : IReviewService
{
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromDays(30);

    public async Task RequestAsync(Booking booking, CancellationToken cancellationToken)
    {
        if (booking.Status != BookingStatus.Completed || booking.Assignment is null)
        {
            return;
        }
        if (booking.PartnerId is { } partnerId
            && await db.Partners.AnyAsync(p => p.Id == partnerId && !p.NotifyPassengers, cancellationToken))
        {
            return;
        }
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.BookingId == booking.Id, cancellationToken);
        if (review is not null)
        {
            // One request per ride; a completed ride is not asked twice.
            return;
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var now = clock.GetUtcNow();
        var channel = string.IsNullOrEmpty(booking.Client.Email) ? NotificationChannel.WhatsApp : NotificationChannel.Email;
        db.Reviews.Add(new Review
        {
            BookingId = booking.Id,
            DriverId = booking.Assignment.DriverId,
            TokenHash = Hash(token),
            Channel = channel,
            SentAt = now,
            ExpiresAt = now + LinkLifetime,
        });
        await db.SaveChangesAsync(cancellationToken);

        await notifications.SendReviewRequestAsync(
            booking, $"{SiteLanguages.BaseUrl(site.Value.FrontendBaseUrl, booking.Client.Language)}/review/{token}", channel, cancellationToken);
    }

    public async Task<ReviewPage> GetAsync(string token, CancellationToken cancellationToken)
    {
        var review = await FindAsync(token, cancellationToken);
        var b = review.Booking;
        return new ReviewPage(
            b.Reference,
            ItalyTime.ToLocal(b.PickupAt),
            b.PickupMeetingPoint?.Name ?? b.Pickup.Address,
            b.DropoffMeetingPoint?.Name ?? b.Dropoff.Address,
            review.Driver.FirstName,
            review.SubmittedAt is not null);
    }

    public async Task SubmitAsync(string token, SubmitReviewRequest request, CancellationToken cancellationToken)
    {
        var review = await FindAsync(token, cancellationToken);
        if (review.SubmittedAt is not null)
        {
            throw new ConflictException("Thank you, this ride has already been rated.");
        }
        review.Rating = request.Rating;
        review.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        review.PublishConsent = request.PublishConsent && review.Comment is not null;
        review.SubmittedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReviewDto>> ListAsync(Guid? driverId, int take, CancellationToken cancellationToken)
    {
        var reviews = await db.Reviews.AsNoTracking()
            .Include(r => r.Booking).ThenInclude(b => b.Client)
            .Include(r => r.Driver)
            .Where(r => driverId == null || r.DriverId == driverId)
            .OrderByDescending(r => r.SentAt)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);
        return reviews.Select(r => new ReviewDto(
            r.Id, r.Booking.Reference, ItalyTime.ToLocal(r.Booking.PickupAt), r.DriverId,
            $"{r.Driver.FirstName} {r.Driver.LastName}", $"{r.Booking.Client.FirstName} {r.Booking.Client.LastName}",
            r.Rating, r.Comment, r.Channel, r.SentAt, r.SubmittedAt)).ToList();
    }

    public async Task<IReadOnlyList<Testimonial>> TestimonialsAsync(CancellationToken cancellationToken)
    {
        var reviews = await db.Reviews.AsNoTracking()
            .Include(r => r.Booking).ThenInclude(b => b.Client)
            .Where(r => r.PublishConsent && r.Rating >= 4 && r.Comment != null)
            .OrderByDescending(r => r.SubmittedAt)
            .Take(12)
            .ToListAsync(cancellationToken);
        return reviews.Select(r => new Testimonial(
            $"{r.Booking.Client.FirstName} {Initial(r.Booking.Client.LastName)}".Trim(),
            r.Rating!.Value,
            r.Comment!,
            DateOnly.FromDateTime(ItalyTime.ToLocal(r.SubmittedAt!.Value)),
            r.Booking.VehicleCategory.ToString())).ToList();
    }

    private static string Initial(string lastName) => lastName.Length > 0 ? $"{char.ToUpperInvariant(lastName[0])}." : "";

    /// <summary>Unknown and expired links give the same answer.</summary>
    private async Task<Review> FindAsync(string token, CancellationToken cancellationToken)
    {
        var hash = Hash(token);
        var review = await db.Reviews
            .Include(r => r.Booking)
            .Include(r => r.Driver)
            .FirstOrDefaultAsync(r => r.TokenHash == hash, cancellationToken);
        if (review is null || (review.SubmittedAt is null && review.ExpiresAt < clock.GetUtcNow()))
        {
            throw new NotFoundException("This review link is not valid or has expired.");
        }
        return review;
    }

    private static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
