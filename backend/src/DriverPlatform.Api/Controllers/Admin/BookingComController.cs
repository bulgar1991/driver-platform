using DriverPlatform.Application.Auth;
using DriverPlatform.Application.BookingCom;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/booking-com")]
[Authorize(Policy = Policies.Admin)]
public sealed class BookingComController(IBookingComSyncService sync, ILogger<BookingComController> logger) : ControllerBase
{
    /// <summary>
    /// Runs one synchronisation round now instead of waiting for the next one. When Booking.com cannot
    /// be reached (or the credentials are missing) the answer is 502 with Booking.com's reason as the title.
    /// </summary>
    [HttpPost("sync")]
    public async Task<ActionResult<BookingComSyncResult>> Sync(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await sync.SyncAsync(cancellationToken));
        }
        catch (Exception ex) when (ex is BookingComApiException or HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Booking.com sync from the admin panel failed");
            return Problem(title: ex is TaskCanceledException ? "Booking.com did not answer in time." : ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
