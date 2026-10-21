using DriverPlatform.Domain.Common;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Domain.Entities;

/// <summary>Sent after drop-off; the link works once.</summary>
public class Review : Entity
{
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public Guid DriverId { get; set; }
    public Driver Driver { get; set; } = null!;
    public string TokenHash { get; set; } = "";
    public NotificationChannel Channel { get; set; }
    public int? Rating { get; set; }
    public string? Comment { get; set; }
    /// <summary>The client agreed that the rating and comment appear on the website with their first name.</summary>
    public bool PublishConsent { get; set; }
    public DateTimeOffset SentAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
}
