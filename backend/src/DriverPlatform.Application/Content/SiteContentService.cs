using System.Text.RegularExpressions;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Dispatch;
using DriverPlatform.Application.Localization;
using DriverPlatform.Application.Common;
using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Content;

/// <summary>A text in the site's four languages; an empty one shows the site's built-in text.</summary>
public sealed record ContentText(string? En, string? It, string? De, string? Fr);

/// <summary>A picture uploaded in the panel (stored as WebP).</summary>
public sealed record ContentImage(string Url, int Width, int Height);

/// <summary>Everything changed in the panel; keys missing here keep the site's own content.</summary>
public sealed record SiteContentDto(
    IReadOnlyDictionary<string, ContentText> Texts,
    IReadOnlyDictionary<string, ContentImage> Images);

public interface ISiteContentService
{
    Task<SiteContentDto> GetAsync(CancellationToken cancellationToken);
    Task<SiteContentDto> SetTextAsync(string key, ContentText text, CancellationToken cancellationToken);
    Task<SiteContentDto> SetImageAsync(string key, byte[] data, CancellationToken cancellationToken);
    Task<SiteContentDto> RemoveImageAsync(string key, CancellationToken cancellationToken);
}

/// <summary>Texts and pictures of the public website, changed in Admin → Website content.</summary>
public sealed partial class SiteContentService(IAppDbContext db, IImageConverter images, IAuditLogger audit) : ISiteContentService
{
    public const int MaxTextLength = 2000;

    public async Task<SiteContentDto> GetAsync(CancellationToken cancellationToken)
    {
        var rows = await db.SiteContents.AsNoTracking().ToListAsync(cancellationToken);
        return new SiteContentDto(
            rows.Where(r => r.En is not null || r.It is not null || r.De is not null || r.Fr is not null)
                .ToDictionary(r => r.Key, r => new ContentText(r.En, r.It, r.De, r.Fr)),
            rows.Where(r => r.PhotoId is not null)
                .ToDictionary(r => r.Key, r => new ContentImage($"/api/photos/{r.PhotoId}", r.ImageWidth ?? 0, r.ImageHeight ?? 0)));
    }

    public async Task<SiteContentDto> SetTextAsync(string key, ContentText text, CancellationToken cancellationToken)
    {
        var row = await FindOrNewAsync(key, cancellationToken);
        row.En = Clean(text.En);
        row.It = Clean(text.It);
        row.De = Clean(text.De);
        row.Fr = Clean(text.Fr);
        audit.Log("content.text", nameof(SiteContent), key, new { row.En, row.It, row.De, row.Fr });
        await SaveAsync(row, cancellationToken);
        return await GetAsync(cancellationToken);
    }

    public async Task<SiteContentDto> SetImageAsync(string key, byte[] data, CancellationToken cancellationToken)
    {
        var row = await FindOrNewAsync(key, cancellationToken);
        var (photo, image) = Photos.Add(db, images, data);
        Photos.Remove(db, row.PhotoId);
        row.PhotoId = photo.Id;
        row.ImageWidth = image.Width;
        row.ImageHeight = image.Height;
        audit.Log("content.image", nameof(SiteContent), key, new { image.Width, image.Height, Bytes = image.Data.Length });
        await SaveAsync(row, cancellationToken);
        return await GetAsync(cancellationToken);
    }

    public async Task<SiteContentDto> RemoveImageAsync(string key, CancellationToken cancellationToken)
    {
        var row = await FindOrNewAsync(key, cancellationToken);
        Photos.Remove(db, row.PhotoId);
        row.PhotoId = null;
        row.ImageWidth = row.ImageHeight = null;
        audit.Log("content.image.removed", nameof(SiteContent), key);
        await SaveAsync(row, cancellationToken);
        return await GetAsync(cancellationToken);
    }

    private async Task<SiteContent> FindOrNewAsync(string key, CancellationToken cancellationToken)
    {
        if (!KeyPattern().IsMatch(key))
        {
            throw new RequestValidationException("key", Texts.Current.ContentKeyInvalid);
        }
        var row = await db.SiteContents.FirstOrDefaultAsync(c => c.Key == key, cancellationToken);
        if (row is null)
        {
            row = new SiteContent { Key = key };
            db.SiteContents.Add(row);
        }
        return row;
    }

    /// <summary>A row with nothing left in it goes: the site shows its own content again.</summary>
    private async Task SaveAsync(SiteContent row, CancellationToken cancellationToken)
    {
        if (row.En is null && row.It is null && row.De is null && row.Fr is null && row.PhotoId is null)
        {
            db.SiteContents.Remove(row);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? Clean(string? value)
    {
        var text = value?.Trim();
        if (text?.Length > MaxTextLength)
        {
            throw new RequestValidationException("text", Texts.Current.ContentTooLong(MaxTextLength));
        }
        return string.IsNullOrEmpty(text) ? null : text;
    }

    [GeneratedRegex("^[a-z0-9]+([.-][a-z0-9]+)*$")]
    private static partial Regex KeyPattern();
}
