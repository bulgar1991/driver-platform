using DriverPlatform.Application.Bookings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DriverPlatform.Infrastructure.Jobs;

/// <summary>Hourly clean-up: cancels bookings that were never paid.</summary>
public sealed class HousekeepingWorker(IServiceScopeFactory scopes, ILogger<HousekeepingWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var cancelled = await scope.ServiceProvider.GetRequiredService<IHousekeepingService>().CancelUnpaidAsync(stoppingToken);
                if (cancelled > 0)
                {
                    logger.LogInformation("Cancelled {Count} unpaid booking(s)", cancelled);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Housekeeping failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
