using DriverPlatform.Application.Abstractions;
using QRCoder;

namespace DriverPlatform.Infrastructure.Security;

/// <summary>QR codes as PNG (QRCoder's PNG writer needs no System.Drawing, so it runs in the Linux container).</summary>
public sealed class QrCodeRenderer : IQrCodeRenderer
{
    public string PngDataUri(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(8);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }
}
