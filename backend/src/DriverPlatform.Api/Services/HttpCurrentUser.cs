using System.IdentityModel.Tokens.Jwt;
using DriverPlatform.Application.Abstractions;

namespace DriverPlatform.Api.Services;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private HttpContext? Context => accessor.HttpContext;

    public Guid? StaffUserId =>
        Guid.TryParse(Context?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null;

    public string? Email => Context?.User.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();
}
