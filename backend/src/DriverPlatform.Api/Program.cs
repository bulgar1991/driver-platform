using System.Globalization;
using System.Text.Json.Serialization;
using DriverPlatform.Api;
using DriverPlatform.Api.Extensions;
using DriverPlatform.Api.Services;
using DriverPlatform.Application;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Localization;
using DriverPlatform.Infrastructure;
using DriverPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddJwtAuth(builder.Configuration);
builder.Services.AddAppRateLimiting();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddOpenApi("internal");
builder.Services.AddPartnerApi();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

// The API runs behind a reverse proxy; the real client IP comes from X-Forwarded-For. Only the proxies
// listed in ForwardedHeaders:KnownNetworks (CIDR) are trusted, otherwise anyone could fake their IP and
// slip past the rate limits. Loopback is always trusted.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    foreach (var network in builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
    {
        o.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    }
});

var app = builder.Build();
app.EnsureProductionReady();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
}

app.UseForwardedHeaders();
// The site sends its language (Accept-Language): client texts and errors come back in it (Texts.Current).
// Only the UI culture changes; numbers and dates in code keep the invariant format.
app.UseRequestLocalization(o =>
{
    o.DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("en"));
    o.SupportedCultures = [CultureInfo.InvariantCulture];
    o.SupportedUICultures = Texts.All.Select(t => CultureInfo.GetCultureInfo(t.Language)).ToList();
    o.RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()];
});
app.UseApiSecurityHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
// The partner API description is public in every environment.
app.MapOpenApi("/api/partner/openapi/{documentName}.json")
    .AddEndpointFilter(async (context, next) =>
        context.HttpContext.Request.RouteValues["documentName"] as string == PartnerApi.Document
            ? await next(context)
            : Results.NotFound());

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UsePasswordChangeGate();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/api/health");

app.Run();

public partial class Program;
