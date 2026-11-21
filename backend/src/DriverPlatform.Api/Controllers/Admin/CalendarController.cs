using DriverPlatform.Application.Auth;
using DriverPlatform.Application.Dispatch;
using DriverPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Controllers.Admin;

/// <summary>The services calendar. Admins and dispatchers; what drivers earn is shown to admins only.</summary>
[ApiController]
[Route("api/admin/calendar")]
[Authorize(Policy = Policies.Staff)]
public sealed class CalendarController(ICalendarService calendar) : ControllerBase
{
    /// <summary>Office dates, inclusive; at most 62 days.</summary>
    [HttpGet]
    public async Task<ActionResult<CalendarResponse>> Get([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await calendar.GetAsync(from, to, User.IsInRole(nameof(StaffRole.Admin)), cancellationToken));
}
