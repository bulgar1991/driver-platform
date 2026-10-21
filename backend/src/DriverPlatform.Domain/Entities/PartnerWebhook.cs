namespace DriverPlatform.Domain.Entities;

/// <summary>
/// Outbox of status notifications to partners. Rows are written in the same transaction as the booking
/// change and delivered in the background with retries, so a slow partner never slows the dispatcher.
/// </summary>
public class PartnerWebhook
{
    public long Id { get; set; }
    public Guid PartnerId { get; set; }
    public Guid BookingId { get; set; }
    public string Event { get; set; } = "";
    public string Payload { get; set; } = "";
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
