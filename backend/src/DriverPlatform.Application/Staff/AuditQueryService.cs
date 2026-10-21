using DriverPlatform.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Staff;

public sealed record AuditEntryDto(
    long Id, DateTimeOffset CreatedAt, string Actor, string Action, string EntityType, string? EntityId, string? Changes, string? IpAddress);

public sealed record AuditPage(IReadOnlyList<AuditEntryDto> Items, long? NextBefore);

public interface IAuditQueryService
{
    /// <param name="before">Continue from this entry id (newest first).</param>
    Task<AuditPage> ListAsync(string? search, long? before, CancellationToken cancellationToken);
}

/// <summary>Business plan §6: who changed what, when, from which IP.</summary>
public sealed class AuditQueryService(IAppDbContext db) : IAuditQueryService
{
    private const int PageSize = 100;

    public async Task<AuditPage> ListAsync(string? search, long? before, CancellationToken cancellationToken)
    {
        var query = db.AuditLogs.AsNoTracking();
        if (before is { } id)
        {
            query = query.Where(a => a.Id < id);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(a => a.Action.ToLower().Contains(term) || a.Actor.ToLower().Contains(term)
                || a.EntityType.ToLower().Contains(term) || (a.EntityId != null && a.EntityId.ToLower().Contains(term))
                || (a.Changes != null && a.Changes.ToLower().Contains(term)));
        }
        var rows = await query.OrderByDescending(a => a.Id).Take(PageSize).ToListAsync(cancellationToken);
        return new AuditPage(
            rows.Select(a => new AuditEntryDto(a.Id, a.CreatedAt, a.Actor, a.Action, a.EntityType, a.EntityId, a.Changes, a.IpAddress)).ToList(),
            rows.Count == PageSize ? rows[^1].Id : null);
    }
}
