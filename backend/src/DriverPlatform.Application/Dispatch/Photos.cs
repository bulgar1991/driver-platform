using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Localization;
using DriverPlatform.Domain.Entities;

namespace DriverPlatform.Application.Dispatch;

/// <summary>
/// Pictures (vehicles, drivers, website images), stored in the database so backups and deploys need
/// nothing extra. Every upload is turned into WebP on the way in.
/// </summary>
public static class Photos
{
    /// <summary>The largest picture accepted (700 KB), uploaded and stored.</summary>
    public const int MaxBytes = 700 * 1024;

    /// <summary>
    /// Checks that the file is a picture (from its bytes: the browser's content type can't be trusted)
    /// of at most 700 KB, converts it to WebP (same size in pixels) and queues the new row.
    /// </summary>
    public static (Photo Photo, WebpImage Image) Add(IAppDbContext db, IImageConverter images, byte[] data)
    {
        if (data.Length > MaxBytes)
        {
            throw new RequestValidationException("file", Texts.Current.PhotoTooLarge);
        }
        var image = images.ToWebp(data, MaxBytes)
            ?? throw new RequestValidationException("file", Texts.Current.PhotoNotAnImage);
        if (image.Data.Length > MaxBytes)
        {
            throw new RequestValidationException("file", Texts.Current.PhotoTooLarge);
        }
        var photo = new Photo { ContentType = "image/webp", Data = image.Data };
        db.Photos.Add(photo);
        return (photo, image);
    }

    /// <summary>Queues the removal of an owner's old picture, if any.</summary>
    public static void Remove(IAppDbContext db, Guid? id)
    {
        if (id is { } photoId)
        {
            db.Photos.Remove(db.Photos.Local.FirstOrDefault(p => p.Id == photoId) ?? new Photo { Id = photoId });
        }
    }
}
