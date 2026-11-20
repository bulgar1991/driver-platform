using DriverPlatform.Application.Abstractions;
using SkiaSharp;

namespace DriverPlatform.Infrastructure.Images;

/// <summary>
/// SkiaSharp (MIT licence, the graphics library of Chrome and Android). Its Linux native library
/// comes with the NuGet package (NativeAssets.Linux.NoDependencies), so the Docker image needs
/// nothing extra.
/// </summary>
public sealed class SkiaImageConverter : IImageConverter
{
    // Good quality first; lower ones only when the file would be over the limit. Transparency (car
    // cut-outs) is kept.
    private static readonly int[] Qualities = [82, 74, 66, 58, 50];

    public WebpImage? ToWebp(byte[] data, int maxBytes)
    {
        // Only real JPEG, PNG and WebP files, whatever the browser said.
        if (!IsAccepted(data))
        {
            return null;
        }
        using var skData = SKData.CreateCopy(data);
        using var codec = SKCodec.Create(skData);
        if (codec is null)
        {
            return null;
        }
        using var decoded = SKBitmap.Decode(codec);
        if (decoded is null)
        {
            return null;
        }
        // Phone photos are stored sideways with a "rotate me" note: turn them for real (no resizing).
        // Re-encoding drops the metadata too (camera details, GPS position).
        using var upright = Upright(decoded, codec.EncodedOrigin);
        using var image = SKImage.FromBitmap(upright);

        byte[] smallest = [];
        foreach (var quality in Qualities)
        {
            using var encoded = image.Encode(SKEncodedImageFormat.Webp, quality);
            if (encoded is null)
            {
                return null;
            }
            smallest = encoded.ToArray();
            if (smallest.Length <= maxBytes)
            {
                break;
            }
        }
        return new WebpImage(smallest, image.Width, image.Height);
    }

    private static bool IsAccepted(byte[] d) => d switch
    {
        [0xFF, 0xD8, 0xFF, ..] => true,
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => true,
        [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] => true,
        _ => false,
    };

    /// <summary>The picture as it should be seen, from the EXIF orientation (a copy; the original is untouched).</summary>
    private static SKBitmap Upright(SKBitmap source, SKEncodedOrigin origin)
    {
        var sideways = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var (width, height) = sideways ? (source.Height, source.Width) : (source.Width, source.Height);
        var result = new SKBitmap(new SKImageInfo(width, height, source.ColorType, source.AlphaType));
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        switch (origin)
        {
            case SKEncodedOrigin.TopRight: // mirrored
                canvas.Translate(width, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight: // upside down
                canvas.Translate(width, height);
                canvas.RotateDegrees(180);
                break;
            case SKEncodedOrigin.BottomLeft: // mirrored upside down
                canvas.Translate(0, height);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftTop: // mirrored and turned
                canvas.Scale(-1, 1);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightTop: // turned left: a phone held upright
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom: // mirrored and turned the other way
                canvas.Translate(width, height);
                canvas.RotateDegrees(90);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.LeftBottom: // turned right
                canvas.Translate(0, height);
                canvas.RotateDegrees(270);
                break;
        }
        canvas.DrawBitmap(source, 0, 0);
        return result;
    }
}
