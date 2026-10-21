using DriverPlatform.Application.Abstractions;
using DriverPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DriverPlatform.Infrastructure.Security;

/// <summary>
/// Caches each user's current stamp for 30 s, so most requests do not query the database. Changes made on
/// this server invalidate the entry at once; with several API servers, others notice within 30 s.
/// </summary>
public sealed class CachedStaffSessions(AppDbContext db, IMemoryCache cache) : IStaffSessions
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

    public async Task<bool> IsValidAsync(Guid userId, string? stamp, CancellationToken cancellationToken)
    {
        if (stamp is null)
        {
            return false;
        }
        var current = await cache.GetOrCreateAsync(Key(userId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            // Empty string: the user does not exist or is inactive.
            return await db.StaffUsers.AsNoTracking()
                .Where(u => u.Id == userId && u.IsActive)
                .Select(u => u.SecurityStamp)
                .FirstOrDefaultAsync(cancellationToken) ?? "";
        });
        return current == stamp;
    }

    public void Invalidate(Guid userId) => cache.Remove(Key(userId));

    private static string Key(Guid userId) => $"staff-session:{userId:N}";
}
