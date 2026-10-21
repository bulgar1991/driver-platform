using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DriverPlatform.Infrastructure.Security;

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    public const string RoleClaim = "role";
    public const string StampClaim = "stamp";
    /// <summary>Present while the user still has a temporary password.</summary>
    public const string PasswordChangeClaim = "pwd_change";

    public AccessToken CreateAccessToken(StaffUser user)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(settings.AccessTokenMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(RoleClaim, user.Role.ToString()),
                new Claim(StampClaim, user.SecurityStamp),
                .. user.MustChangePassword ? [new Claim(PasswordChangeClaim, "1")] : Array.Empty<Claim>(),
            ],
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        var handler = new JwtSecurityTokenHandler();
        handler.OutboundClaimTypeMap.Clear();
        return new AccessToken(handler.WriteToken(token), expires);
    }
}
