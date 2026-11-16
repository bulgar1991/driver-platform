using DriverPlatform.Application.BookingCom;
using DriverPlatform.Application.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DriverPlatform.Infrastructure.BookingCom;

/// <summary>Fetches Booking.com rides and sends our answers back every <see cref="BookingComOptions.PollSeconds"/>.</summary>
public sealed class BookingComWorker(IServiceScopeFactory scopes, IOptions<BookingComOptions> options, ILogger<BookingComWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(15, options.Value.PollSeconds)));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<IBookingComSyncService>().SyncAsync(stoppingToken);
                if (result.Imported + result.Amended + result.Cancelled + result.Accepted + result.Rejected + result.DriversSent + result.EventsSent + result.Errors > 0)
                {
                    logger.LogInformation("Booking.com sync: {Result}", result);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Booking.com sync failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
