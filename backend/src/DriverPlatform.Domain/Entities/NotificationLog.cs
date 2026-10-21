using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Domain.Entities;

/// <summary>Every WhatsApp or email message sent to a driver or client, so the dispatcher can see what arrived.</summary>
public class NotificationLog
{
    public long Id { get; set; }
    public NotificationKind Kind { get; set; }
    public NotificationChannel Channel { get; set; }
    public NotificationStatus Status { get; set; }
    /// <summary>Phone number or email address.</summary>
    public string Recipient { get; set; } = "";
    public Guid? DriverId { get; set; }
    public Guid? AssignmentId { get; set; }
    public Guid? BookingId { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
