using DriverPlatform.Application.Auth;
using DriverPlatform.Application.Visitors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Controllers.Admin;

/// <summary>Admin → Visitors: who visited the public site, from where.</summary>
[ApiController]
[Route("api/admin/visitors")]
[Authorize(Policy = Policies.Admin)]
public sealed class VisitorsController(IVisitorService visitors) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<VisitorSummary>> Get([FromQuery] int days = 30, CancellationToken cancellationToken = default) =>
        Ok(await visitors.SummaryAsync(days, cancellationToken));
}
