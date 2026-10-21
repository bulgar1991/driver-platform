using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Infrastructure.Auditing;
using DriverPlatform.Infrastructure.Documents;
using DriverPlatform.Infrastructure.Email;
using DriverPlatform.Infrastructure.Jobs;
using DriverPlatform.Infrastructure.Maps;
using DriverPlatform.Infrastructure.Partners;
using DriverPlatform.Infrastructure.Payments;
using DriverPlatform.Infrastructure.Persistence;
using DriverPlatform.Infrastructure.Security;
using DriverPlatform.Infrastructure.WhatsApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DriverPlatform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(configuration.GetConnectionString("Database"))
            .UseSnakeCaseNamingConvention());
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<DbSeeder>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.SigningKey.Length >= 32, "Jwt:SigningKey must be at least 32 characters.")
            .ValidateOnStart();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IStaffSessions, CachedStaffSessions>();
        services.AddScoped<IAuditLogger, AuditLogger>();

        AddPayments(services, configuration);

        services.AddScoped<IPartnerWebhookDispatcher, PartnerWebhookDispatcher>();
        services.AddHttpClient(PartnerWebhookDispatcher.HttpClientName, http =>
        {
            http.Timeout = TimeSpan.FromSeconds(10);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("MagnusDrive-Webhooks/1.0");
        });
        if (configuration.GetValue("Webhooks:Enabled", true))
        {
            services.AddHostedService<PartnerWebhookWorker>();
        }
        if (configuration.GetValue("Housekeeping:Enabled", true))
        {
            services.AddHostedService<HousekeepingWorker>();
        }

        services.AddMemoryCache();
        services.Configure<MapsOptions>(configuration.GetSection(MapsOptions.SectionName));
        services.AddHttpClient<IRoutingService, OsrmRoutingService>((sp, http) =>
            ConfigureMapsClient(http, sp.GetRequiredService<IOptions<MapsOptions>>().Value, o => o.OsrmBaseUrl));
        services.AddHttpClient<IGeocodingService, NominatimGeocodingService>((sp, http) =>
            ConfigureMapsClient(http, sp.GetRequiredService<IOptions<MapsOptions>>().Value, o => o.NominatimBaseUrl));

        return services;
    }

    private static void AddPayments(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PaymentsOptions>()
            .Bind(configuration.GetSection(PaymentsOptions.SectionName))
            .Validate(o => o.Provider is "Stripe" or "Fake", "Payments:Provider must be \"Stripe\" or \"Fake\".")
            .Validate(o => Uri.IsWellFormedUriString(o.FrontendBaseUrl, UriKind.Absolute), "Payments:FrontendBaseUrl must be an absolute URL.")
            .ValidateOnStart();
        services.AddOptions<LinksOptions>()
            .Bind(configuration.GetSection(LinksOptions.SectionName))
            .Validate(o => o.SigningKey.Length >= 32, "Links:SigningKey must be at least 32 characters.")
            .ValidateOnStart();
        services.Configure<CompanyOptions>(configuration.GetSection(CompanyOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));

        if (configuration[$"{PaymentsOptions.SectionName}:Provider"] == "Stripe")
        {
            services.AddOptions<StripeOptions>()
                .Bind(configuration.GetSection(StripeOptions.SectionName))
                .Validate(o => o.SecretKey.StartsWith("sk_") || o.SecretKey.StartsWith("rk_"), "Payments:Stripe:SecretKey is missing.")
                .Validate(o => o.WebhookSecret.StartsWith("whsec_"), "Payments:Stripe:WebhookSecret is missing.")
                .ValidateOnStart();
            services.AddSingleton<IPaymentGateway, StripePaymentGateway>();
        }
        else
        {
            services.AddSingleton<FakePaymentGateway>();
            services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<FakePaymentGateway>());
        }

        services.AddSingleton<IInvoiceRenderer, QuestInvoiceRenderer>();
        services.AddSingleton<IEmailSender, MailKitEmailSender>();

        services.AddOptions<WhatsAppOptions>()
            .Bind(configuration.GetSection(WhatsAppOptions.SectionName))
            .Validate(o => o.Provider is "Meta" or "Log", "WhatsApp:Provider must be \"Meta\" or \"Log\".")
            .Validate(o => o.Provider != "Meta" || (o.PhoneNumberId.Length > 0 && o.AccessToken.Length > 0),
                "WhatsApp:PhoneNumberId and WhatsApp:AccessToken are required for the Meta provider.")
            .ValidateOnStart();
        if (configuration[$"{WhatsAppOptions.SectionName}:Provider"] == "Meta")
        {
            services.AddHttpClient<IWhatsAppSender, MetaWhatsAppSender>(http =>
            {
                http.BaseAddress = new Uri("https://graph.facebook.com/");
                http.Timeout = TimeSpan.FromSeconds(15);
            });
        }
        else
        {
            services.AddSingleton<IWhatsAppSender, LogWhatsAppSender>();
        }
    }

    private static void ConfigureMapsClient(HttpClient http, MapsOptions options, Func<MapsOptions, string> baseUrl)
    {
        http.BaseAddress = new Uri(baseUrl(options).TrimEnd('/') + "/");
        http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        http.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
    }
}
