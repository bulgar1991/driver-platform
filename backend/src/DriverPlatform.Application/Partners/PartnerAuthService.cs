using DriverPlatform.Application.Abstractions;
using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Partners;

public interface IPartnerAuthService
{
    /// <summary>The active partner owning this API key, or null.</summary>
    Task<Partner?> AuthenticateAsync(string apiKey, CancellationToken cancellationToken);
}

public sealed class PartnerAuthService(IAppDbContext db, IPartnerKeys keys, TimeProvider clock) : IPartnerAuthService
{
    /// <summary>"Last used" is written at most this often, to keep API calls read-only most of the time.</summary>
    private static readonly TimeSpan TouchEvery = TimeSpan.FromMinutes(5);

    public async Task<Partner?> AuthenticateAsync(string apiKey, CancellationToken cancellationToken)
    {
        var prefix = keys.PrefixOf(apiKey.Trim());
        if (prefix is null)
        {
            return null;
        }
        var partner = await db.Partners.FirstOrDefaultAsync(p => p.ApiKeyPrefix == prefix && p.IsActive, cancellationToken);
        if (partner?.ApiKeyHash is null || !keys.Matches(apiKey.Trim(), partner.ApiKeyHash))
        {
            return null;
        }

        var now = clock.GetUtcNow();
        if (partner.ApiKeyLastUsedAt is null || now - partner.ApiKeyLastUsedAt > TouchEvery)
        {
            partner.ApiKeyLastUsedAt = now;
            await db.SaveChangesAsync(cancellationToken);
        }
        return partner;
    }
}
