using DriverPlatform.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DriverPlatform.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        return result.Outcome switch
        {
            LoginOutcome.Success => Ok(result.Response),
            LoginOutcome.LockedOut => Problem(
                title: "Account temporarily locked. Try again later.",
                statusCode: StatusCodes.Status423Locked),
            _ => Problem(title: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized),
        };
    }

    /// <summary>Changes the signed-in user's password and returns a fresh token; other sessions end.</summary>
    [HttpPost("change-password")]
    [Authorize(Policy = Policies.Staff)]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<LoginResponse>> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.Identity?.Name, out var id))
        {
            return Unauthorized();
        }
        return Ok(await authService.ChangePasswordAsync(id, request, cancellationToken));
    }

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
}
