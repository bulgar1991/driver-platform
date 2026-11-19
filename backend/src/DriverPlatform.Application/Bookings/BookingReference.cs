using System.Security.Cryptography;

namespace DriverPlatform.Application.Bookings;

public static class BookingReference
{
    // No 0/O or 1/I/L, so it can be read out over the phone.
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    /// <summary>The brand's letters before the code, e.g. "MD" → "MD-7K3QX9"; set once at startup from Company:ReferencePrefix.</summary>
    public static string Prefix { get; private set; } = "MD";

    public static void UsePrefix(string? prefix) => Prefix = NormalizePrefix(prefix);

    /// <summary>"hv " → "HV"; empty → "MD"; anything but 2–4 letters is refused.</summary>
    public static string NormalizePrefix(string? prefix)
    {
        var value = string.IsNullOrWhiteSpace(prefix) ? "MD" : prefix.Trim().ToUpperInvariant();
        if (value.Length is < 2 or > 4 || !value.All(char.IsAsciiLetterUpper))
        {
            throw new InvalidOperationException("Company:ReferencePrefix must be 2 to 4 letters, e.g. \"MD\".");
        }
        return value;
    }

    public static string Create() => Create(Prefix);

    public static string Create(string prefix) => prefix + "-" + RandomNumberGenerator.GetString(Alphabet, 6);
}
