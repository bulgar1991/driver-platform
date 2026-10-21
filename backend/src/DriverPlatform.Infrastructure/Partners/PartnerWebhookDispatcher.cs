using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DriverPlatform.Application.Partners;
using DriverPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DriverPlatform.Infrastructure.Partners;

public interface IPartnerWebhookDispatcher
{
    /// <summary>Sends the webhooks that are due; returns how many were delivered.</summary>
    Task<int> DeliverDueAsync(CancellationToken cancellationToken);
}

/// <summary>
/// POSTs the JSON payload with "MagnusDrive-Signature: t={unix},v1={hex HMAC-SHA256(secret, "{t}.{body}")}".
/// Retries after 1, 2, 4 … 128 minutes, then gives up (the panel shows the error).
/// </summary>
public sealed class PartnerWebhookDispatcher(
    AppDbContext db,
    IHttpClientFactory httpClients,
    IPartnerKeys keys,
    TimeProvider clock,
    ILogger<PartnerWebhookDispatcher> logger) : IPartnerWebhookDispatcher
{
    public const string HttpClientName = "partner-webhooks";
    public const int MaxAttempts = 8;
    private const int Batch = 20;

    public async Task<int> DeliverDueAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var due = await db.PartnerWebhooks
            .Where(w => w.DeliveredAt == null && w.Attempts < MaxAttempts && w.NextAttemptAt <= now)
            .OrderBy(w => w.Id)
            .Take(Batch)
            .ToListAsync(cancellationToken);
        if (due.Count == 0)
        {
            return 0;
        }

        var partnerIds = due.Select(w => w.PartnerId).Distinct().ToList();
        var partners = await db.Partners.AsNoTracking().Where(p => partnerIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);
        var http = httpClients.CreateClient(HttpClientName);
        var delivered = 0;

        foreach (var webhook in due)
        {
            webhook.Attempts++;
            if (!partners.TryGetValue(webhook.PartnerId, out var partner) || partner.WebhookUrl is null || !partner.IsActive)
            {
                webhook.LastError = "No webhook URL";
                webhook.Attempts = MaxAttempts;
                continue;
            }
            try
            {
                var timestamp = clock.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
                var signature = Convert.ToHexStringLower(HMACSHA256.HashData(
                    Encoding.UTF8.GetBytes(keys.WebhookSecret(partner)),
                    Encoding.UTF8.GetBytes($"{timestamp}.{webhook.Payload}")));

                using var request = new HttpRequestMessage(HttpMethod.Post, partner.WebhookUrl)
                {
                    Content = new StringContent(webhook.Payload, Encoding.UTF8, "application/json"),
                };
                request.Headers.Add("MagnusDrive-Event", webhook.Event);
                request.Headers.Add("MagnusDrive-Delivery", webhook.Id.ToString(CultureInfo.InvariantCulture));
                request.Headers.Add("MagnusDrive-Signature", $"t={timestamp},v1={signature}");
                using var response = await http.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    webhook.DeliveredAt = clock.GetUtcNow();
                    webhook.LastError = null;
                    delivered++;
                    continue;
                }
                webhook.LastError = $"HTTP {(int)response.StatusCode}";
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                webhook.LastError = ex.Message.Length > 480 ? ex.Message[..480] : ex.Message;
            }
            webhook.NextAttemptAt = clock.GetUtcNow().AddMinutes(Math.Pow(2, webhook.Attempts - 1));
            logger.LogWarning("Webhook {Id} to {Partner} failed ({Error}), attempt {Attempt}", webhook.Id, partner.Name, webhook.LastError, webhook.Attempts);
        }

        await db.SaveChangesAsync(cancellationToken);
        return delivered;
    }
}
