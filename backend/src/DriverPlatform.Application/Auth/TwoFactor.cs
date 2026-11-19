using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using DriverPlatform.Application.Common;
using Microsoft.Extensions.Options;

namespace DriverPlatform.Application.Auth;

/// <summary>
/// Time-based one-time codes (RFC 6238, as in Google Authenticator, Microsoft Authenticator, Authy):
/// HMAC-SHA1, 6 digits, a new code every 30 seconds.
/// </summary>
public static class Totp
{
    public const int Digits = 6;
    public static readonly TimeSpan Period = TimeSpan.FromSeconds(30);
    /// <summary>The phone's clock may be a little off: the previous and next codes are accepted too.</summary>
    public const int Window = 1;

    public static byte[] NewSecret() => RandomNumberGenerator.GetBytes(20);

    public static long StepAt(DateTimeOffset time) => time.ToUnixTimeSeconds() / (long)Period.TotalSeconds;

    public static string Code(byte[] secret, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        var hash = HMACSHA1.HashData(secret, counter);
        var offset = hash[^1] & 0x0F;
        var value = (BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset, 4)) & 0x7FFFFFFF) % 1_000_000;
        return value.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The time step the code belongs to, if it is valid now and newer than <paramref name="lastUsedStep"/>.</summary>
    public static long? Verify(byte[] secret, string code, DateTimeOffset now, long lastUsedStep)
    {
        var current = StepAt(now);
        for (var step = current - Window; step <= current + Window; step++)
        {
            if (step > lastUsedStep && CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(Code(secret, step)), Encoding.ASCII.GetBytes(code)))
            {
                return step;
            }
        }
        return null;
    }

    /// <summary>The address an authenticator app reads from the QR code.</summary>
    public static string SetupUri(string issuer, string account, byte[] secret) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}" +
        $"?secret={Base32.Encode(secret)}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={(int)Period.TotalSeconds}";
}

/// <summary>RFC 4648 base32 without padding: how authenticator apps take a key typed by hand.</summary>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(ReadOnlySpan<byte> data)
    {
        var output = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                output.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }
        if (bits > 0)
        {
            output.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        }
        return output.ToString();
    }
}

/// <summary>
/// Keeps the two-step secrets: the authenticator secret is stored encrypted, recovery codes only as hashes,
/// and the step between password and code is a short signed token. Keys are derived from
/// <see cref="LinksOptions.SigningKey"/>, so changing it turns two-step sign-in off for everyone.
/// </summary>
public sealed class TwoFactorKeys(IOptions<LinksOptions> links)
{
    public static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);
    private const string RecoveryAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public string Protect(byte[] secret)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[secret.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(Key("staff-totp-secret-v1"), tag.Length);
        aes.Encrypt(nonce, secret, cipher, tag);
        return Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    public byte[] Unprotect(string protectedSecret)
    {
        var data = Convert.FromBase64String(protectedSecret);
        var secret = new byte[data.Length - 28];
        using var aes = new AesGcm(Key("staff-totp-secret-v1"), 16);
        aes.Decrypt(data.AsSpan(0, 12), data.AsSpan(28), data.AsSpan(12, 16), secret);
        return secret;
    }

    /// <summary>Proves the password was right; carries the security stamp, so a password change voids it.</summary>
    public string CreateChallenge(Guid userId, string securityStamp, DateTimeOffset expiresAt)
    {
        var payload = $"{userId:N}.{expiresAt.ToUnixTimeSeconds()}.{securityStamp}";
        return Base64Url(Encoding.UTF8.GetBytes(payload)) + "." + Base64Url(Sign(payload));
    }

    /// <summary>The user and stamp of a valid, unexpired challenge; null otherwise.</summary>
    public (Guid UserId, string SecurityStamp)? ReadChallenge(string token, DateTimeOffset now)
    {
        var parts = token.Split('.');
        if (parts.Length != 2)
        {
            return null;
        }
        try
        {
            var payload = Encoding.UTF8.GetString(FromBase64Url(parts[0]));
            if (!CryptographicOperations.FixedTimeEquals(Sign(payload), FromBase64Url(parts[1])))
            {
                return null;
            }
            var fields = payload.Split('.');
            if (fields.Length != 3 || !Guid.TryParseExact(fields[0], "N", out var userId)
                || !long.TryParse(fields[1], out var expires) || DateTimeOffset.FromUnixTimeSeconds(expires) < now)
            {
                return null;
            }
            return (userId, fields[2]);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>Ten codes like "K7QX-M2PA", shown once; each works once instead of the app's code.</summary>
    public static IReadOnlyList<string> NewRecoveryCodes() =>
        Enumerable.Range(0, 10)
            .Select(_ => $"{RandomNumberGenerator.GetString(RecoveryAlphabet, 4)}-{RandomNumberGenerator.GetString(RecoveryAlphabet, 4)}")
            .ToList();

    /// <summary>Spaces, dashes and case do not matter when typing a recovery code.</summary>
    public static string HashRecoveryCode(string code) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            new string(code.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant())));

    /// <summary>Email codes are stored only as an HMAC tied to the user.</summary>
    public string HashEmailCode(Guid userId, string code) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Key("staff-2fa-email-v1"), Encoding.UTF8.GetBytes($"{userId:N}:{code}")));

    private byte[] Sign(string payload) => HMACSHA256.HashData(Key("staff-2fa-challenge-v1"), Encoding.UTF8.GetBytes(payload));

    private byte[] Key(string purpose) =>
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(links.Value.SigningKey), Encoding.UTF8.GetBytes(purpose));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s + new string('=', (4 - s.Length % 4) % 4));
    }
}
