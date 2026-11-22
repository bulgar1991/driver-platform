using DriverPlatform.Application.Auth;
using DriverPlatform.Application.Statistics;
using DriverPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Controllers.Admin;

/// <summary>Admin → Statistics. Driver pay is for admins only.</summary>
[ApiController]
[Route("api/admin/statistics")]
[Authorize(Policy = Policies.Staff)]
public sealed class StatisticsController(IStatisticsService statistics) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<StatisticsResult>> Get([FromQuery] StatisticsQuery query, CancellationToken cancellationToken) =>
        Ok(await statistics.GetAsync(query, User.IsInRole(nameof(StaffRole.Admin)), cancellationToken));
}
