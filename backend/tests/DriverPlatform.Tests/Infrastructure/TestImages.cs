using SkiaSharp;

namespace DriverPlatform.Tests.Infrastructure;

/// <summary>Real little pictures for upload tests.</summary>
public static class TestImages
{
    private static readonly SKColor Gold = new(201, 164, 92);

    public static byte[] Png(int width, int height) => Encode(Plain(width, height), SKEncodedImageFormat.Png);

    public static byte[] Jpeg(int width, int height) => Encode(Plain(width, height), SKEncodedImageFormat.Jpeg);

    /// <summary>Left half red, right half blue: shows which way a picture was turned.</summary>
    public static byte[] HalvesJpeg(int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Blue);
            using var red = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(0, 0, width / 2f, height, red);
        }
        return Encode(bitmap, SKEncodedImageFormat.Jpeg);
    }

    /// <summary>The same JPEG with an EXIF note "turn 90° clockwise to view" (orientation 6), as phones write it.</summary>
    public static byte[] WithOrientation(byte[] jpeg, ushort orientation)
    {
        byte[] tiff =
        [
            (byte)'M', (byte)'M', 0, 42, 0, 0, 0, 8, // big-endian header, first IFD at 8
            0, 1, // one entry
            0x01, 0x12, 0, 3, 0, 0, 0, 1, (byte)(orientation >> 8), (byte)orientation, 0, 0, // Orientation, SHORT, 1
            0, 0, 0, 0, // no next IFD
        ];
        byte[] exif = [(byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0, .. tiff];
        var length = exif.Length + 2;
        byte[] app1 = [0xFF, 0xE1, (byte)(length >> 8), (byte)length, .. exif];
        return [jpeg[0], jpeg[1], .. app1, .. jpeg[2..]];
    }

    /// <summary>Random pixels: the hardest case for compression.</summary>
    public static byte[] NoisyPng(int width, int height)
    {
        var random = new Random(42);
        var bitmap = new SKBitmap(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)));
            }
        }
        return Encode(bitmap, SKEncodedImageFormat.Png);
    }

    public static bool IsWebp(byte[] data) =>
        data is [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..];

    public static SKColor PixelAt(byte[] image, int x, int y)
    {
        using var bitmap = SKBitmap.Decode(image);
        return bitmap.GetPixel(x, y);
    }

    private static SKBitmap Plain(int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        bitmap.Erase(Gold);
        return bitmap;
    }

    private static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format)
    {
        using (bitmap)
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(format, 95))
        {
            return data.ToArray();
        }
    }
}
