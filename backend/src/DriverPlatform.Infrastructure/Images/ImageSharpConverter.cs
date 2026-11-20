using DriverPlatform.Application.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace DriverPlatform.Infrastructure.Images;

/// <summary>
/// ImageSharp (Six Labors Split License: free under USD 1M annual revenue, like QuestPDF; see
/// sixlabors.com/pricing). Pure .NET, so the Docker image needs no extra system libraries.
/// </summary>
public sealed class ImageSharpConverter : IImageConverter
{
    private static readonly IImageFormat[] Accepted = [JpegFormat.Instance, PngFormat.Instance, WebpFormat.Instance];

    // Good quality first; lower ones only when the file would be over the limit. Transparency (car
    // cut-outs) is kept.
    private static readonly int[] Qualities = [82, 74, 66, 58, 50];

    public WebpImage? ToWebp(byte[] data, int maxBytes)
    {
        IImageFormat format;
        try
        {
            format = Image.DetectFormat(data);
        }
        catch (Exception e) when (e is UnknownImageFormatException or InvalidImageContentException)
        {
            return null;
        }
        if (!Accepted.Contains(format))
        {
            return null;
        }

        try
        {
            using var image = Image.Load(data);
            // Phone photos are stored sideways with a "rotate me" note: turn them for real (no resizing).
            image.Mutate(x => x.AutoOrient());
            // No camera details or GPS position in a public picture.
            image.Metadata.ExifProfile = null;
            image.Metadata.XmpProfile = null;
            image.Metadata.IptcProfile = null;

            byte[] smallest = [];
            foreach (var quality in Qualities)
            {
                using var output = new MemoryStream();
                image.Save(output, new WebpEncoder { FileFormat = WebpFileFormatType.Lossy, Quality = quality });
                smallest = output.ToArray();
                if (smallest.Length <= maxBytes)
                {
                    break;
                }
            }
            return new WebpImage(smallest, image.Width, image.Height);
        }
        catch (Exception e) when (e is InvalidImageContentException or UnknownImageFormatException or NotSupportedException)
        {
            return null;
        }
    }
}
