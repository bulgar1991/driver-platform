namespace DriverPlatform.Api.Extensions;

public static class SecurityHeaders
{
    /// <summary>
    /// API responses are JSON or files, never pages: nothing may frame, sniff or cache them.
    /// The site's own headers (CSP, HSTS) are set by the web server in front of the SPA (deploy/Caddyfile).
    /// </summary>
    public static IApplicationBuilder UseApiSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
                if (!headers.ContainsKey("Cache-Control"))
                {
                    headers.CacheControl = "no-store";
                }
                return Task.CompletedTask;
            });
            await next();
        });
}
