using DriverPlatform.Application.Auth;
using DriverPlatform.Application.Dispatch;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Controllers.Admin;

/// <summary>Admin → Expirations: every deadline of the drivers and cars, and renewing one in place.</summary>
[ApiController]
[Route("api/admin/expirations")]
[Authorize(Policy = Policies.Staff)]
public sealed class ExpirationsController(IExpirationService expirations) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ExpirationsResponse>> List([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        Ok(await expirations.ListAsync(includeInactive, cancellationToken));

    [HttpPut("renew")]
    public async Task<IActionResult> Renew(RenewDeadlineRequest request, CancellationToken cancellationToken)
    {
        await expirations.RenewAsync(request, cancellationToken);
        return NoContent();
    }
}
