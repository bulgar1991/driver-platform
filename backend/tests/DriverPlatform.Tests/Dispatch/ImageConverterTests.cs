using DriverPlatform.Application.Dispatch;
using DriverPlatform.Infrastructure.Images;
using DriverPlatform.Tests.Infrastructure;

namespace DriverPlatform.Tests.Dispatch;

public sealed class ImageConverterTests
{
    private readonly SkiaImageConverter _converter = new();

    [Fact]
    public void Jpeg_and_png_are_stored_as_webp_at_their_own_size()
    {
        foreach (var upload in new[] { TestImages.Jpeg(1800, 900), TestImages.Png(40, 30) })
        {
            var image = _converter.ToWebp(upload, Photos.MaxBytes)!;
            Assert.True(TestImages.IsWebp(image.Data));
            Assert.True(image.Data.Length <= Photos.MaxBytes);
        }
        // Never resized.
        var large = _converter.ToWebp(TestImages.Png(3200, 1800), Photos.MaxBytes)!;
        Assert.Equal((3200, 1800), (large.Width, large.Height));
    }

    [Fact]
    public void Quality_goes_down_until_the_file_fits()
    {
        var noisy = TestImages.NoisyPng(400, 300);

        var best = _converter.ToWebp(noisy, int.MaxValue)!;
        var limited = _converter.ToWebp(noisy, best.Data.Length - 1)!;

        Assert.True(limited.Data.Length < best.Data.Length);
        Assert.Equal((400, 300), (limited.Width, limited.Height));
    }

    [Fact]
    public void Something_that_is_not_a_picture_is_refused()
    {
        Assert.Null(_converter.ToWebp("<svg onload=alert(1)>"u8.ToArray(), Photos.MaxBytes));
        Assert.Null(_converter.ToWebp([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3], Photos.MaxBytes));
    }

    [Fact]
    public void A_phone_photo_taken_upright_is_stored_upright()
    {
        // 40×30, red on the left; the note says "turn 90° clockwise": the left side goes to the top.
        var upload = TestImages.WithOrientation(TestImages.HalvesJpeg(40, 30), 6);

        var image = _converter.ToWebp(upload, Photos.MaxBytes)!;

        Assert.Equal((30, 40), (image.Width, image.Height));
        var top = TestImages.PixelAt(image.Data, 15, 5);
        var bottom = TestImages.PixelAt(image.Data, 15, 34);
        Assert.True(top.Red > 180 && top.Blue < 90, $"top {top}");
        Assert.True(bottom.Blue > 180 && bottom.Red < 90, $"bottom {bottom}");
    }
}
