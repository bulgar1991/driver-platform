namespace DriverPlatform.Application.Abstractions;

/// <summary>A picture ready to store: WebP, same width and height as uploaded.</summary>
public sealed record WebpImage(byte[] Data, int Width, int Height);

/// <summary>Every uploaded picture is stored as WebP: smaller files, the same format everywhere.</summary>
public interface IImageConverter
{
    /// <summary>
    /// A JPEG, PNG or WebP picture, turned upright (phone photos), without its metadata (camera, GPS
    /// position) and saved as WebP at its own size: never resized. The quality goes down only when the
    /// file would be over <paramref name="maxBytes"/>; it can still be over when even the lowest
    /// quality is not enough. Null when the bytes are not such a picture.
    /// </summary>
    WebpImage? ToWebp(byte[] data, int maxBytes);
}
