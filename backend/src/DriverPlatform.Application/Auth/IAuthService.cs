namespace DriverPlatform.Application.Auth;

public interface IAuthService
{
    /// <summary>Signs in, or asks for the second step when two-step sign-in is on.</summary>
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    /// <summary>The second step: the authenticator code or a recovery code.</summary>
    Task<LoginResult> LoginTwoFactorAsync(TwoFactorLoginRequest request, CancellationToken cancellationToken);
    /// <summary>Emails a sign-in code (at most one a minute); false when the challenge is no longer valid.</summary>
    Task<bool> SendEmailCodeAsync(TwoFactorEmailRequest request, CancellationToken cancellationToken);
    Task<CurrentUserResponse?> GetUserAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>Saves the signed-in user's own order of the panel menu.</summary>
    Task SetMenuOrderAsync(Guid userId, MenuOrderRequest request, CancellationToken cancellationToken);
    /// <summary>Returns a new token: the old ones stop working because the security stamp changes.</summary>
    Task<LoginResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken);

    Task<TwoFactorStatus> TwoFactorStatusAsync(Guid userId, CancellationToken cancellationToken);
    /// <summary>A new secret to scan; two-step sign-in starts only once <see cref="EnableTwoFactorAsync"/> confirms a code.</summary>
    Task<TwoFactorSetupResponse> SetUpTwoFactorAsync(Guid userId, CancellationToken cancellationToken);
    Task<TwoFactorEnabledResponse> EnableTwoFactorAsync(Guid userId, TwoFactorCodeRequest request, CancellationToken cancellationToken);
    /// <summary>Other sessions end; when two-step sign-in is required, the new session opens only the setup.</summary>
    Task<LoginResponse> DisableTwoFactorAsync(Guid userId, TwoFactorDisableRequest request, CancellationToken cancellationToken);
    /// <summary>New recovery codes; the old ones stop working.</summary>
    Task<RecoveryCodesResponse> NewRecoveryCodesAsync(Guid userId, TwoFactorCodeRequest request, CancellationToken cancellationToken);
}
