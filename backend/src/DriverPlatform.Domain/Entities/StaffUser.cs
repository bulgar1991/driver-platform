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
}
