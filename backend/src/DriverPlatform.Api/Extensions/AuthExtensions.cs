using System.IdentityModel.Tokens.Jwt;
using System.Text;
using DriverPlatform.Application.Auth;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using DriverPlatform.Application.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace DriverPlatform.Api.Extensions;

public static class AuthExtensions
{
    public static IServiceCollection AddJwtAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.Events = new JwtBearerEvents { OnTokenValidated = ValidateSessionAsync };
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = JwtTokenService.RoleClaim,
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Staff, p => p.RequireRole(nameof(StaffRole.Admin), nameof(StaffRole.Dispatcher)))
            .AddPolicy(Policies.Admin, p => p.RequireRole(nameof(StaffRole.Admin)));

        return services;
    }

    /// <summary>Rejects tokens of deactivated users and tokens issued before a password or role change.</summary>
    private static async Task ValidateSessionAsync(TokenValidatedContext context)
    {
        var principal = context.Principal!;
        var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var stamp = principal.FindFirst(JwtTokenService.StampClaim)?.Value;
        if (!Guid.TryParse(sub, out var userId))
        {
            context.Fail("Invalid token.");
            return;
        }

        var sessions = context.HttpContext.RequestServices.GetRequiredService<IStaffSessions>();
        if (!await sessions.IsValidAsync(userId, stamp, context.HttpContext.RequestAborted))
        {
            context.Fail("Session ended.");
        }
    }

    /// <summary>With a temporary password, only /api/auth (change password, me) is reachable.</summary>
    public static IApplicationBuilder UsePasswordChangeGate(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.User.HasClaim(JwtTokenService.PasswordChangeClaim, "1")
                && !context.Request.Path.StartsWithSegments("/api/auth"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { title = "Change your temporary password first.", status = 403 });
                return;
            }
            await next();
        });
}
