using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.DriverPortal;
using DriverPlatform.Application.Geo;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Dispatch;

public interface IDriverAdminService
{
    Task<IReadOnlyList<DriverDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<DriverDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<DriverDto> CreateAsync(DriverRequest request, CancellationToken cancellationToken);
    Task<DriverDto> UpdateAsync(Guid id, DriverRequest request, CancellationToken cancellationToken);
    Task<DriverLinkResponse> GetLinkAsync(Guid id, CancellationToken cancellationToken);
    Task<DriverLinkResponse> RotateLinkAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Drivers are deactivated, never deleted: past shifts and reviews keep pointing at them.</summary>
public sealed class DriverAdminService(IAppDbContext db, IDriverLinks links, IAuditLogger audit, TimeProvider clock) : IDriverAdminService
{
    public async Task<IReadOnlyList<DriverDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var drivers = await db.Drivers.AsNoTracking().Include(d => d.Documents)
            .Where(d => includeInactive || d.IsActive)
            .OrderBy(d => d.LastName).ThenBy(d => d.FirstName)
            .ToListAsync(cancellationToken);
        var stats = await StatsAsync(drivers.Select(d => d.Id).ToList(), cancellationToken);
        return drivers.Select(d => ToDto(d, stats.GetValueOrDefault(d.Id))).ToList();
    }

    public async Task<DriverDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await ToDtoAsync(await FindAsync(id, cancellationToken), cancellationToken);

    public async Task<DriverLinkResponse> GetLinkAsync(Guid id, CancellationToken cancellationToken)
    {
        var driver = await FindAsync(id, cancellationToken);
        var url = links.Url(driver);
        await db.SaveChangesAsync(cancellationToken);
        return new DriverLinkResponse(url);
    }

    public async Task<DriverLinkResponse> RotateLinkAsync(Guid id, CancellationToken cancellationToken)
    {
        var driver = await FindAsync(id, cancellationToken);
        var url = links.Rotate(driver);
        audit.Log("driver.link_rotated", nameof(Driver), id.ToString(), new { driver.LinkVersion });
        await db.SaveChangesAsync(cancellationToken);
        return new DriverLinkResponse(url);
    }

    private sealed record Stats(decimal? Rating, int RatingCount, int CompletedRides, int CancelledShifts);

    /// <summary>Business plan §7: client ratings, number of rides and cancellations per driver.</summary>
    private async Task<Dictionary<Guid, Stats>> StatsAsync(List<Guid> ids, CancellationToken cancellationToken)
    {
        var ratings = await db.Reviews.AsNoTracking()
            .Where(r => ids.Contains(r.DriverId) && r.Rating != null)
            .GroupBy(r => r.DriverId)
            .Select(g => new { DriverId = g.Key, Average = g.Average(r => (double)r.Rating!), Count = g.Count() })
            .ToListAsync(cancellationToken);
        var rides = await db.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.Completed && b.Assignment != null && ids.Contains(b.Assignment.DriverId))
            .GroupBy(b => b.Assignment!.DriverId)
            .Select(g => new { DriverId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var cancelled = await db.Assignments.AsNoTracking()
            .Where(a => a.Status == AssignmentStatus.Cancelled && ids.Contains(a.DriverId))
            .GroupBy(a => a.DriverId)
            .Select(g => new { DriverId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return ids.ToDictionary(id => id, id =>
        {
            var r = ratings.FirstOrDefault(x => x.DriverId == id);
            return new Stats(
                r is null ? null : Math.Round((decimal)r.Average, 1),
                r?.Count ?? 0,
                rides.FirstOrDefault(x => x.DriverId == id)?.Count ?? 0,
                cancelled.FirstOrDefault(x => x.DriverId == id)?.Count ?? 0);
        });
    }

    private async Task<DriverDto> ToDtoAsync(Driver driver, CancellationToken cancellationToken) =>
        ToDto(driver, (await StatsAsync([driver.Id], cancellationToken)).GetValueOrDefault(driver.Id));

    public async Task<DriverDto> CreateAsync(DriverRequest request, CancellationToken cancellationToken)
    {
        await EnsureUniquePhoneAsync(request.Phone, null, cancellationToken);
        var driver = new Driver();
        Apply(driver, request);
        links.EnsureHash(driver);
        db.Drivers.Add(driver);

        audit.Log("driver.created", nameof(Driver), driver.Id.ToString(), new { request.FirstName, request.LastName });
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(driver, cancellationToken);
    }

    public async Task<DriverDto> UpdateAsync(Guid id, DriverRequest request, CancellationToken cancellationToken)
    {
        var driver = await FindAsync(id, cancellationToken);
        await EnsureUniquePhoneAsync(request.Phone, id, cancellationToken);
        Apply(driver, request);

        audit.Log("driver.updated", nameof(Driver), id.ToString(), new
        {
            request.IsActive,
            request.IsAvailable,
            Documents = request.Documents.Select(d => new { d.Type, d.ExpiresOn }),
        });
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(driver, cancellationToken);
    }

    private async Task<Driver> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Drivers.Include(d => d.Documents).FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
        ?? throw new NotFoundException("Driver not found.");

    private async Task EnsureUniquePhoneAsync(string phone, Guid? exceptId, CancellationToken cancellationToken)
    {
        var normalized = NormalizePhone(phone);
        if (await db.Drivers.AnyAsync(d => d.Phone == normalized && d.Id != exceptId, cancellationToken))
        {
            throw new RequestValidationException("phone", "Another driver already has this phone number.");
        }
    }

    private void Apply(Driver driver, DriverRequest request)
    {
        driver.FirstName = request.FirstName.Trim();
        driver.LastName = request.LastName.Trim();
        driver.Phone = NormalizePhone(request.Phone);
        driver.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        driver.Languages = request.Languages.Select(l => l.Trim().ToLowerInvariant()).Where(l => l.Length > 0).Distinct().ToList();
        driver.PreferredAreas = request.PreferredAreas.Select(a => a.Trim()).Where(a => a.Length > 0).Distinct().ToList();
        driver.CommissionPercent = request.CommissionPercent;
        driver.IsActive = request.IsActive;
        driver.IsAvailable = request.IsAvailable;
        driver.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        // Documents are replaced as a list: kept by id, the others added or removed.
        var keep = request.Documents.Where(d => d.Id is not null).Select(d => d.Id!.Value).ToHashSet();
        foreach (var removed in driver.Documents.Where(d => !keep.Contains(d.Id)).ToList())
        {
            driver.Documents.Remove(removed);
            db.DriverDocuments.Remove(removed);
        }
        foreach (var input in request.Documents)
        {
            var document = input.Id is { } existingId ? driver.Documents.FirstOrDefault(d => d.Id == existingId) : null;
            if (document is null)
            {
                document = new DriverDocument { DriverId = driver.Id };
                driver.Documents.Add(document);
                db.DriverDocuments.Add(document);
            }
            document.Type = input.Type;
            document.Number = string.IsNullOrWhiteSpace(input.Number) ? null : input.Number.Trim();
            document.ExpiresOn = input.ExpiresOn;
        }
    }

    private DriverDto ToDto(Driver d, Stats? stats) => new(
        d.Id, d.FirstName, d.LastName, d.Phone, d.Email, d.Languages, d.PreferredAreas, d.CommissionPercent,
        d.IsActive, d.IsAvailable, d.Notes,
        d.Documents.OrderBy(x => x.Type).Select(x => new DriverDocumentDto { Id = x.Id, Type = x.Type, Number = x.Number, ExpiresOn = x.ExpiresOn }).ToList(),
        DocumentRules.AlertCount(DocumentRules.Dated(d), Today()),
        stats?.Rating,
        stats?.RatingCount ?? 0,
        stats?.CompletedRides ?? 0,
        stats?.CancelledShifts ?? 0);

    private DateOnly Today() => DateOnly.FromDateTime(ItalyTime.ToLocal(clock.GetUtcNow()));

    internal static string NormalizePhone(string phone) =>
        new(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
}
