using System.Threading.RateLimiting;

namespace DriverPlatform.Api;

public static class RateLimitPolicies
{
    public const string Login = "login";
    /// <summary>Address search and price quotes from the public booking form.</summary>
    public const string Public = "public";
    public const string Booking = "booking";
    /// <summary>Website forms that send us an email (driver applications).</summary>
    public const string Form = "form";
    /// <summary>Per API key, not per IP: a partner's servers may share addresses.</summary>
    public const string Partner = "partner";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Login, context => PerIp(context, 10));
            options.AddPolicy(Public, context => PerIp(context, 60));
            options.AddPolicy(Booking, context => PerIp(context, 10));
            options.AddPolicy(Form, context => PerIp(context, 5));
            options.AddPolicy(Partner, context => RateLimitPartition.GetFixedWindowLimiter(
                "key:" + (context.Request.Headers["X-Api-Key"].ToString() is { Length: > 11 } key ? key[..11] : "none"),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));
        });
        return services;
    }

    private static RateLimitPartition<string> PerIp(HttpContext context, int permitsPerMinute) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitsPerMinute, Window = TimeSpan.FromMinutes(1) });
}
