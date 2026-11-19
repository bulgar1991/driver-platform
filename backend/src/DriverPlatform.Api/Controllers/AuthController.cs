using DriverPlatform.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DriverPlatform.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>
    /// 200 with a <see cref="LoginResponse"/>, or, when two-step sign-in is on, 200 with a
    /// <see cref="TwoFactorChallengeResponse"/> (<c>twoFactorRequired: true</c>) to send to <c>login/two-factor</c>.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.LoginAsync(request, cancellationToken));

    /// <summary>The second step: the 6-digit code from the authenticator app, or a recovery code.</summary>
    [HttpPost("login/two-factor")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> LoginTwoFactor(TwoFactorLoginRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.LoginTwoFactorAsync(request, cancellationToken));

    /// <summary>Emails a sign-in code instead of the app's code (at most one a minute).</summary>
    [HttpPost("login/two-factor/email")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> SendEmailCode(TwoFactorEmailRequest request, CancellationToken cancellationToken) =>
        await authService.SendEmailCodeAsync(request, cancellationToken)
            ? NoContent()
            : Problem(title: "Sign in again: the 5 minutes for the code ran out.", statusCode: StatusCodes.Status401Unauthorized,
                type: "two-factor-expired");

    /// <summary>Changes the signed-in user's password and returns a fresh token; other sessions end.</summary>
    [HttpPost("change-password")]
    [Authorize(Policy = Policies.Staff)]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<LoginResponse>> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken) =>
        Ok(await authService.ChangePasswordAsync(UserId, request, cancellationToken));

    [HttpGet("me")]
    [Authorize(Policy = Policies.Staff)]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.Identity?.Name, out var id))
        {
            return Unauthorized();
        }
        var user = await authService.GetUserAsync(id, cancellationToken);
        return user is null ? Unauthorized() : Ok(user);
    }

    [HttpGet("two-factor")]
    [Authorize(Policy = Policies.Staff)]
    public async Task<ActionResult<TwoFactorStatus>> TwoFactorStatus(CancellationToken cancellationToken) =>
        Ok(await authService.TwoFactorStatusAsync(UserId, cancellationToken));

    /// <summary>Starts the setup: the QR code and key to add to the authenticator app.</summary>
    [HttpPost("two-factor/setup")]
    [Authorize(Policy = Policies.Staff)]
    public async Task<ActionResult<TwoFactorSetupResponse>> SetUpTwoFactor(CancellationToken cancellationToken) =>
        Ok(await authService.SetUpTwoFactorAsync(UserId, cancellationToken));

    /// <summary>Confirms the app works with its first code; returns the recovery codes (shown only here) and a new session.</summary>
    [HttpPost("two-factor/enable")]
    [Authorize(Policy = Policies.Staff)]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<TwoFactorEnabledResponse>> EnableTwoFactor(TwoFactorCodeRequest request, CancellationToken cancellationToken) =>
        Ok(await authService.EnableTwoFactorAsync(UserId, request, cancellationToken));

    [HttpPost("two-factor/disable")]
    [Authorize(Policy = Policies.Staff)]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<LoginResponse>> DisableTwoFactor(TwoFactorDisableRequest request, CancellationToken cancellationToken) =>
        Ok(await authService.DisableTwoFactorAsync(UserId, request, cancellationToken));

    [HttpPost("two-factor/recovery-codes")]
    [Authorize(Policy = Policies.Staff)]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<RecoveryCodesResponse>> NewRecoveryCodes(TwoFactorCodeRequest request, CancellationToken cancellationToken) =>
        Ok(await authService.NewRecoveryCodesAsync(UserId, request, cancellationToken));

    private Guid UserId => Guid.TryParse(User.Identity?.Name, out var id) ? id : throw new UnauthorizedAccessException();

    private IActionResult ToActionResult(LoginResult result) => result.Outcome switch
    {
        LoginOutcome.Success => Ok(result.Response),
        LoginOutcome.TwoFactorRequired => Ok(result.Challenge),
        LoginOutcome.LockedOut => Problem(
            title: "Account temporarily locked. Try again later.",
            statusCode: StatusCodes.Status423Locked),
        LoginOutcome.InvalidCode => Problem(title: "The code is not correct.", statusCode: StatusCodes.Status401Unauthorized,
            type: "two-factor-code"),
        LoginOutcome.ChallengeExpired => Problem(title: "Sign in again: the 5 minutes for the code ran out.",
            statusCode: StatusCodes.Status401Unauthorized, type: "two-factor-expired"),
        _ => Problem(title: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized),
    };
}
