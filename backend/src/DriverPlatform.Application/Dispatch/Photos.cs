using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Localization;
using DriverPlatform.Domain.Entities;

namespace DriverPlatform.Application.Dispatch;

/// <summary>Vehicle and driver pictures, stored in the database so backups and deploys need nothing extra.</summary>
public static class Photos
{
    /// <summary>The panel shrinks pictures before sending them; this only stops mistakes.</summary>
    public const int MaxBytes = 5 * 1024 * 1024;

    /// <summary>Checks the file's first bytes (the browser's content type can't be trusted) and queues the new row.</summary>
    public static Photo Add(IAppDbContext db, byte[] data)
    {
        if (data.Length > MaxBytes)
        {
            throw new RequestValidationException("file", Texts.Current.PhotoTooLarge);
        }
        var photo = new Photo
        {
            ContentType = ContentType(data) ?? throw new RequestValidationException("file", Texts.Current.PhotoNotAnImage),
            Data = data,
        };
        db.Photos.Add(photo);
        return photo;
    }

    /// <summary>Queues the removal of an owner's old picture, if any.</summary>
    public static void Remove(IAppDbContext db, Guid? id)
    {
        if (id is { } photoId)
        {
            db.Photos.Remove(db.Photos.Local.FirstOrDefault(p => p.Id == photoId) ?? new Photo { Id = photoId });
        }
    }

    private static string? ContentType(byte[] d) => d switch
    {
        [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => "image/png",
        [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] => "image/webp",
        _ => null,
    };
}
