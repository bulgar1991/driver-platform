using DriverPlatform.Domain.Common;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Domain.Entities;

/// <summary>Admins and dispatchers who sign in to the management panel.</summary>
public class StaffUser : Entity
{
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public StaffRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public int FailedLoginCount { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    /// <summary>Changes on password change, deactivation or role change; tokens carrying an older stamp stop working.</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>Set for temporary passwords: the panel opens only the password page until it is changed.</summary>
    public bool MustChangePassword { get; set; }
    public DateTimeOffset? PasswordChangedAt { get; set; }

    /// <summary>Two-step sign-in is on: the authenticator app's code is asked after the password.</summary>
    public DateTimeOffset? TwoFactorEnabledAt { get; set; }
    /// <summary>The authenticator app's secret, encrypted (AES-GCM); null while two-step sign-in is off.</summary>
    public string? TwoFactorSecret { get; set; }
    /// <summary>A secret being set up, until the first code confirms the app has it.</summary>
    public string? TwoFactorPendingSecret { get; set; }
    /// <summary>The 30-second time step of the last accepted code, so a code is never accepted twice.</summary>
    public long TwoFactorLastStep { get; set; }
    /// <summary>SHA-256 of the unused one-time recovery codes.</summary>
    public List<string> TwoFactorRecoveryCodes { get; set; } = [];
    /// <summary>HMAC of the code last sent by email, while it is valid.</summary>
    public string? TwoFactorEmailCodeHash { get; set; }
    public DateTimeOffset? TwoFactorEmailCodeExpiresAt { get; set; }
    /// <summary>When the last email code was sent: at most one a minute.</summary>
    public DateTimeOffset? TwoFactorEmailCodeSentAt { get; set; }
}
