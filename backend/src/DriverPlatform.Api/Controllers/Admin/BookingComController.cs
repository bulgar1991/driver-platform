using DriverPlatform.Application.Auth;
using DriverPlatform.Application.BookingCom;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/booking-com")]
[Authorize(Policy = Policies.Admin)]
public sealed class BookingComController(IBookingComSyncService sync) : ControllerBase
{
    /// <summary>Runs one synchronisation round now instead of waiting for the next one.</summary>
    [HttpPost("sync")]
    public async Task<ActionResult<BookingComSyncResult>> Sync(CancellationToken cancellationToken) =>
        Ok(await sync.SyncAsync(cancellationToken));
}
