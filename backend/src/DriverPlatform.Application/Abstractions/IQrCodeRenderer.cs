namespace DriverPlatform.Application.Abstractions;

public interface IQrCodeRenderer
{
    /// <summary>The text as a QR code, as a "data:image/png;base64,…" address an img tag can show.</summary>
    string PngDataUri(string text);
}
