using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record CurrentUserResponse(Guid Id, string Email, string FullName, StaffRole Role, bool MustChangePassword = false);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, CurrentUserResponse User);

public enum LoginOutcome
{
    Success,
    InvalidCredentials,
    LockedOut,
}

public sealed record LoginResult(LoginOutcome Outcome, LoginResponse? Response = null);
