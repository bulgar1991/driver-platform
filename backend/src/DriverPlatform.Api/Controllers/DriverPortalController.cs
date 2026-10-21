using DriverPlatform.Application.DriverPortal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DriverPlatform.Api.Controllers;

/// <summary>The driver's personal link. The token in the URL is the only credential: no login on the phone.</summary>
[ApiController]
[Route("api/driver/{token}")]
[EnableRateLimiting(RateLimitPolicies.Public)]
public sealed class DriverPortalController(IDriverPortalService portal) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DriverPortalResponse>> Get(string token, CancellationToken cancellationToken) =>
        Ok(await portal.GetAsync(token, cancellationToken));

    [HttpPost("shifts/{shiftId:guid}/confirm")]
    public async Task<ActionResult<DriverPortalResponse>> Confirm(string token, Guid shiftId, CancellationToken cancellationToken) =>
        Ok(await portal.ConfirmShiftAsync(token, shiftId, cancellationToken));

    [HttpPost("bookings/{bookingId:guid}/picked-up")]
    public async Task<ActionResult<DriverPortalResponse>> PickedUp(string token, Guid bookingId, CancellationToken cancellationToken) =>
        Ok(await portal.PickedUpAsync(token, bookingId, cancellationToken));

    [HttpPost("bookings/{bookingId:guid}/completed")]
    public async Task<ActionResult<DriverPortalResponse>> Completed(string token, Guid bookingId, CancellationToken cancellationToken) =>
        Ok(await portal.CompletedAsync(token, bookingId, cancellationToken));
}
