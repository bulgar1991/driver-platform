using DriverPlatform.Application.Common;
using DriverPlatform.Application.Localization;

namespace DriverPlatform.Application.Auth;

/// <summary>Length over complexity rules (NIST SP 800-63B): at least 12 characters, not obvious.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 12;

    private static readonly string[] Common =
    [
        "password", "123456", "qwerty", "magnusdrive", "magnus", "admin", "letmein", "welcome", "changeme", "iloveyou",
    ];

    public static void Validate(string password, string email)
    {
        var errors = new ValidationErrors();
        if (password.Length < MinLength)
        {
            errors.Add("newPassword", Texts.Current.PasswordTooShort(MinLength));
        }
        var lower = password.ToLowerInvariant();
        var local = email.Split('@')[0].ToLowerInvariant();
        if ((local.Length >= 4 && lower.Contains(local)) || Common.Any(lower.Contains) || password.Distinct().Count() < 4)
        {
            errors.Add("newPassword", Texts.Current.PasswordTooEasy);
        }
        errors.ThrowIfAny();
    }

    /// <summary>16 characters without look-alikes, for a first sign-in or a reset.</summary>
    public static string Temporary() =>
        System.Security.Cryptography.RandomNumberGenerator.GetString("abcdefghjkmnpqrstuvwxyzABCDEFGHJKMNPQRSTUVWXYZ23456789", 16);
}
