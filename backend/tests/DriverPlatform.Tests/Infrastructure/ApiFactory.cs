using DriverPlatform.Application.Abstractions;
using DriverPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DriverPlatform.Tests.Infrastructure;

/// <summary>Runs the real API with an in-memory database and the seeded admin.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "correct-horse-battery";

    private readonly string _databaseName = $"tests-{Guid.NewGuid()}";

    public FakeRoutingService Routing { get; } = new();
    public CapturingEmailSender Emails { get; } = new();
    public CapturingWhatsAppSender WhatsApp { get; } = new();
    public FakeWebhookReceiver WebhookReceiver { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-0123456789abcdef0123456789");
        builder.UseSetting("Bootstrap:AdminEmail", AdminEmail);
        builder.UseSetting("Bootstrap:AdminPassword", AdminPassword);
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("Links:SigningKey", "test-links-key-0123456789abcdef0123456789");
        builder.UseSetting("Payments:Provider", "Fake");
        builder.UseSetting("Payments:FrontendBaseUrl", "https://magnus.test");
        builder.UseSetting("Company:Name", "Magnus Drive Test");
        builder.UseSetting("Company:Email", "office@magnus.test");
        builder.UseSetting("WhatsApp:Provider", "Log");
        // Tests deliver webhooks explicitly through IPartnerWebhookDispatcher.
        builder.UseSetting("Webhooks:Enabled", "false");
        builder.UseSetting("Housekeeping:Enabled", "false");
        // TestServer requests have no remote address; trust any so X-Forwarded-For tests work.
        builder.UseSetting("ForwardedHeaders:KnownNetworks:0", "0.0.0.0/0");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<AppDbContext>));
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IRoutingService>();
            services.RemoveAll<IGeocodingService>();
            services.AddSingleton<IRoutingService>(Routing);
            services.AddSingleton<IGeocodingService, FakeGeocodingService>();
            services.AddHttpClient(DriverPlatform.Infrastructure.Partners.PartnerWebhookDispatcher.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => WebhookReceiver);
            services.RemoveAll<IWhatsAppSender>();
            services.AddSingleton<IWhatsAppSender>(WhatsApp);
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync().GetAwaiter().GetResult();
    }
}
