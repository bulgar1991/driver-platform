using System.ComponentModel.DataAnnotations;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record CurrentUserResponse(
    Guid Id, string Email, string FullName, StaffRole Role, bool MustChangePassword = false, bool TwoFactorEnabled = false,
    /// <summary>Two-step sign-in is required and not set up yet: only My account opens.</summary>
    bool TwoFactorSetupRequired = false,
    /// <summary>The panel menu in the user's order (page paths); empty: the built-in order.</summary>
    IReadOnlyList<string>? MenuOrder = null);

/// <summary>The panel menu's pages ("/admin/calendar"…) in the order to show them; empty: the built-in order.</summary>
public sealed record MenuOrderRequest
{
    [MaxLength(MenuOrderRequest.MaxItems)]
    public List<string> Order { get; init; } = [];

    public const int MaxItems = 60;
}

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, CurrentUserResponse User);

/// <summary>The password was right; send the authenticator code (or a recovery code) with this token.</summary>
public sealed record TwoFactorChallengeResponse(string ChallengeToken, DateTimeOffset ExpiresAt, bool TwoFactorRequired = true);

public sealed record TwoFactorLoginRequest(string ChallengeToken, string Code);

/// <summary>Sends a sign-in code by email instead of the app's code.</summary>
public sealed record TwoFactorEmailRequest(string ChallengeToken);

public enum LoginOutcome
{
    Success,
    InvalidCredentials,
    LockedOut,
    /// <summary>Password right, two-step sign-in on: <see cref="LoginResult.Challenge"/> is set.</summary>
    TwoFactorRequired,
    InvalidCode,
    /// <summary>The 5 minutes between password and code ran out (or the password changed meanwhile).</summary>
    ChallengeExpired,
}

public sealed record LoginResult(LoginOutcome Outcome, LoginResponse? Response = null, TwoFactorChallengeResponse? Challenge = null);

public sealed record TwoFactorStatus(bool Enabled, DateTimeOffset? EnabledAt, int RecoveryCodesLeft);

/// <param name="Secret">The key for typing into the app by hand, in groups of four.</param>
/// <param name="SetupUri">otpauth:// address, the same as the QR code.</param>
/// <param name="QrCode">PNG data URI of the QR code to scan.</param>
public sealed record TwoFactorSetupResponse(string Secret, string SetupUri, string QrCode);

public sealed record TwoFactorCodeRequest(string Code);

public sealed record TwoFactorDisableRequest(string Password, string Code);

/// <summary>Shown once: only their hashes are kept.</summary>
public sealed record RecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes);

/// <summary>The recovery codes (shown once) and a new session that opens the whole panel.</summary>
public sealed record TwoFactorEnabledResponse(IReadOnlyList<string> RecoveryCodes, LoginResponse Session);
