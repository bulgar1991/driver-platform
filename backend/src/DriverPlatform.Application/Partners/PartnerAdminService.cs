using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Payments;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using DriverPlatform.Application.Localization;

namespace DriverPlatform.Application.Partners;

public sealed record PartnerRequest
{
    [Required, MaxLength(120)]
    public required string Name { get; init; }
    public PartnerKind Kind { get; init; }
    [Required, EmailAddress, MaxLength(254)]
    public required string ContactEmail { get; init; }
    [Range(-50, 50)]
    public decimal PriceAdjustmentPercent { get; init; }
    [Range(0, 50)]
    public decimal CommissionPercent { get; init; }
    public bool MonthlyInvoicing { get; init; } = true;
    public bool NotifyPassengers { get; init; } = true;
    [MaxLength(500)]
    public string? WebhookUrl { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record PartnerDto(
    Guid Id,
    string Name,
    PartnerKind Kind,
    string ContactEmail,
    string? ApiKeyPrefix,
    DateTimeOffset? ApiKeyCreatedAt,
    DateTimeOffset? ApiKeyLastUsedAt,
    decimal PriceAdjustmentPercent,
    decimal CommissionPercent,
    bool MonthlyInvoicing,
    bool NotifyPassengers,
    string? WebhookUrl,
    bool IsActive,
    int BookingsThisMonth,
    int WebhooksFailing);

/// <summary>Shown once; only the prefix and a hash are kept.</summary>
public sealed record PartnerApiKeyResponse(string ApiKey, string Prefix);

public sealed record PartnerSecretResponse(string Secret);

public sealed record WebhookDeliveryDto(
    long Id, string Event, string BookingReference, int Attempts, DateTimeOffset CreatedAt, DateTimeOffset? DeliveredAt, string? LastError);

public interface IPartnerAdminService
{
    Task<IReadOnlyList<PartnerDto>> ListAsync(CancellationToken cancellationToken);
    Task<PartnerDto> CreateAsync(PartnerRequest request, CancellationToken cancellationToken);
    Task<PartnerDto> UpdateAsync(Guid id, PartnerRequest request, CancellationToken cancellationToken);
    Task<PartnerApiKeyResponse> CreateKeyAsync(Guid id, CancellationToken cancellationToken);
    Task RevokeKeyAsync(Guid id, CancellationToken cancellationToken);
    Task<PartnerSecretResponse> WebhookSecretAsync(Guid id, bool rotate, CancellationToken cancellationToken);
    Task<IReadOnlyList<WebhookDeliveryDto>> DeliveriesAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>Monthly statement (CSV): rides, prices and commission, for invoicing the partner.</summary>
    Task<InvoiceFile> StatementAsync(Guid id, int year, int month, CancellationToken cancellationToken);
}

public sealed class PartnerAdminService(IAppDbContext db, IPartnerKeys keys, IAuditLogger audit, TimeProvider clock) : IPartnerAdminService
{
    public async Task<IReadOnlyList<PartnerDto>> ListAsync(CancellationToken cancellationToken)
    {
        var partners = await db.Partners.AsNoTracking().OrderBy(p => p.Name).ToListAsync(cancellationToken);
        var result = new List<PartnerDto>();
        foreach (var partner in partners)
        {
            result.Add(await ToDtoAsync(partner, cancellationToken));
        }
        return result;
    }

    public async Task<PartnerDto> CreateAsync(PartnerRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        var partner = new Partner();
        Apply(partner, request);
        db.Partners.Add(partner);
        audit.Log("partner.created", nameof(Partner), partner.Id.ToString(), new { request.Name, request.Kind });
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(partner, cancellationToken);
    }

    public async Task<PartnerDto> UpdateAsync(Guid id, PartnerRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        var partner = await FindAsync(id, cancellationToken);
        Apply(partner, request);
        audit.Log("partner.updated", nameof(Partner), id.ToString(), request);
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(partner, cancellationToken);
    }

    public async Task<PartnerApiKeyResponse> CreateKeyAsync(Guid id, CancellationToken cancellationToken)
    {
        var partner = await FindAsync(id, cancellationToken);
        var key = keys.Create();
        // Replaces the previous key at once.
        partner.ApiKeyPrefix = key.Prefix;
        partner.ApiKeyHash = key.Hash;
        partner.ApiKeyCreatedAt = clock.GetUtcNow();
        partner.ApiKeyLastUsedAt = null;
        audit.Log("partner.api_key_created", nameof(Partner), id.ToString(), new { key.Prefix });
        await db.SaveChangesAsync(cancellationToken);
        return new PartnerApiKeyResponse(key.Key, key.Prefix);
    }

    public async Task RevokeKeyAsync(Guid id, CancellationToken cancellationToken)
    {
        var partner = await FindAsync(id, cancellationToken);
        audit.Log("partner.api_key_revoked", nameof(Partner), id.ToString(), new { partner.ApiKeyPrefix });
        partner.ApiKeyPrefix = null;
        partner.ApiKeyHash = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PartnerSecretResponse> WebhookSecretAsync(Guid id, bool rotate, CancellationToken cancellationToken)
    {
        var partner = await FindAsync(id, cancellationToken);
        if (rotate)
        {
            partner.SecretVersion++;
            audit.Log("partner.webhook_secret_rotated", nameof(Partner), id.ToString());
            await db.SaveChangesAsync(cancellationToken);
        }
        return new PartnerSecretResponse(keys.WebhookSecret(partner));
    }

    public async Task<IReadOnlyList<WebhookDeliveryDto>> DeliveriesAsync(Guid id, CancellationToken cancellationToken)
    {
        var rows = await db.PartnerWebhooks.AsNoTracking()
            .Where(w => w.PartnerId == id)
            .OrderByDescending(w => w.Id)
            .Take(30)
            .ToListAsync(cancellationToken);
        var ids = rows.Select(r => r.BookingId).Distinct().ToList();
        var references = await db.Bookings.Where(b => ids.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Reference, cancellationToken);
        return rows.Select(r => new WebhookDeliveryDto(
            r.Id, r.Event, references.GetValueOrDefault(r.BookingId, "?"), r.Attempts, r.CreatedAt, r.DeliveredAt, r.LastError)).ToList();
    }

    public async Task<InvoiceFile> StatementAsync(Guid id, int year, int month, CancellationToken cancellationToken)
    {
        var partner = await FindAsync(id, cancellationToken);
        var first = new DateOnly(year, month, 1);
        var start = ItalyTime.ToUtc(first.ToDateTime(TimeOnly.MinValue))!.Value;
        var end = ItalyTime.ToUtc(first.AddMonths(1).ToDateTime(TimeOnly.MinValue))!.Value;
        var bookings = await db.Bookings.AsNoTracking()
            .Include(b => b.Client)
            .Where(b => b.PartnerId == id && b.PickupAt >= start && b.PickupAt < end)
            .OrderBy(b => b.PickupAt)
            .ToListAsync(cancellationToken);

        var it = CultureInfo.GetCultureInfo("it-IT");
        string Money(decimal v) => v.ToString("0.00", it);
        var csv = new StringBuilder("reference;external_reference;pickup;passenger;from;to;vehicle;status;price;commission;net\n");
        decimal total = 0, commission = 0;
        foreach (var b in bookings)
        {
            // Cancelled rides are listed for reference but not charged.
            var charged = b.Status == BookingStatus.Cancelled ? 0 : b.TotalPrice;
            var fee = Math.Round(charged * partner.CommissionPercent / 100, 2, MidpointRounding.AwayFromZero);
            total += charged;
            commission += fee;
            csv.AppendJoin(';',
                b.Reference,
                Csv(b.ExternalReference ?? ""),
                ItalyTime.ToLocal(b.PickupAt).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                Csv($"{b.Client.FirstName} {b.Client.LastName}"),
                Csv(b.PickupMeetingPoint?.Name ?? b.Pickup.Address),
                Csv(b.DropoffMeetingPoint?.Name ?? b.Dropoff.Address),
                b.VehicleCategory,
                b.Status,
                Money(charged),
                Money(fee),
                Money(charged - fee)).Append('\n');
        }
        csv.Append(CultureInfo.InvariantCulture, $"TOTAL;;;;;;;;{Money(total)};{Money(commission)};{Money(total - commission)}\n");

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        var slug = new string(partner.Name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        return new InvoiceFile($"statement-{slug}-{year}-{month:00}.csv", bytes);
    }

    private static void Validate(PartnerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.WebhookUrl))
        {
            return;
        }
        var ok = Uri.TryCreate(request.WebhookUrl.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback));
        if (!ok)
        {
            throw new RequestValidationException("webhookUrl", Texts.Current.HttpsOnly);
        }
    }

    private static void Apply(Partner partner, PartnerRequest request)
    {
        partner.Name = request.Name.Trim();
        partner.Kind = request.Kind;
        partner.ContactEmail = request.ContactEmail.Trim().ToLowerInvariant();
        partner.PriceAdjustmentPercent = request.PriceAdjustmentPercent;
        partner.CommissionPercent = request.CommissionPercent;
        partner.MonthlyInvoicing = request.MonthlyInvoicing;
        partner.NotifyPassengers = request.NotifyPassengers;
        partner.WebhookUrl = string.IsNullOrWhiteSpace(request.WebhookUrl) ? null : request.WebhookUrl.Trim();
        partner.IsActive = request.IsActive;
    }

    private async Task<Partner> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Partners.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
        ?? throw new NotFoundException(Texts.Current.PartnerNotFound);

    private async Task<PartnerDto> ToDtoAsync(Partner p, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(ItalyTime.ToLocal(clock.GetUtcNow()));
        var monthStart = ItalyTime.ToUtc(new DateOnly(today.Year, today.Month, 1).ToDateTime(TimeOnly.MinValue))!.Value;
        var bookings = await db.Bookings.CountAsync(b => b.PartnerId == p.Id && b.CreatedAt >= monthStart && b.ReturnOfBookingId == null, cancellationToken);
        var failing = await db.PartnerWebhooks.CountAsync(w => w.PartnerId == p.Id && w.DeliveredAt == null && w.Attempts > 0, cancellationToken);
        return new PartnerDto(
            p.Id, p.Name, p.Kind, p.ContactEmail, p.ApiKeyPrefix, p.ApiKeyCreatedAt, p.ApiKeyLastUsedAt,
            p.PriceAdjustmentPercent, p.CommissionPercent, p.MonthlyInvoicing, p.NotifyPassengers, p.WebhookUrl, p.IsActive,
            bookings, failing);
    }

    private static string Csv(string value)
    {
        if (value.Length > 0 && "=+-@".Contains(value[0]))
        {
            value = "'" + value;
        }
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
