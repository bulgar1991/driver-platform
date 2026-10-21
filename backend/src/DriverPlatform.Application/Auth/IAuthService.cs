namespace DriverPlatform.Application.Auth;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<CurrentUserResponse?> GetUserAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>Returns a new token: the old ones stop working because the security stamp changes.</summary>
    Task<LoginResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken);
}
