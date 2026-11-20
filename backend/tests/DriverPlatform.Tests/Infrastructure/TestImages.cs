using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DriverPlatform.Tests.Infrastructure;

/// <summary>Real little pictures for upload tests.</summary>
public static class TestImages
{
    public static byte[] Png(int width, int height) => Save(Plain(width, height), (i, s) => i.SaveAsPng(s));

    public static byte[] Jpeg(int width, int height) => Save(Plain(width, height), (i, s) => i.SaveAsJpeg(s));

    /// <summary>Random pixels: the hardest case for compression.</summary>
    public static byte[] NoisyPng(int width, int height)
    {
        var random = new Random(42);
        var image = new Image<Rgb24>(width, height);
        image.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                var row = rows.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    row[x] = new Rgb24((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
                }
            }
        });
        return Save(image, (i, s) => i.SaveAsPng(s));
    }

    public static bool IsWebp(byte[] data) =>
        data is [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..];

    private static Image<Rgb24> Plain(int width, int height) => new(width, height, new Rgb24(201, 164, 92));

    private static byte[] Save(Image image, Action<Image, Stream> save)
    {
        using (image)
        {
            using var stream = new MemoryStream();
            save(image, stream);
            return stream.ToArray();
        }
    }
}
