using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DriverPlatform.Infrastructure.Partners;

/// <summary>Delivers partner webhooks every few seconds, outside the requests that caused them.</summary>
public sealed class PartnerWebhookWorker(IServiceScopeFactory scopes, ILogger<PartnerWebhookWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IPartnerWebhookDispatcher>().DeliverDueAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Partner webhook delivery failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
