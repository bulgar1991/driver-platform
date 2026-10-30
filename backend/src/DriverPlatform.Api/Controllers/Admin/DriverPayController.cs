using DriverPlatform.Application.Auth;
using DriverPlatform.Application.Dispatch;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Controllers.Admin;

/// <summary>Driver salaries: pay per ride and the monthly totals. Admins only.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = Policies.Admin)]
public sealed class DriverPayController(IDriverPayService pay) : ControllerBase
{
    [HttpPut("bookings/{id:guid}/driver-pay")]
    public async Task<ActionResult<DriverPayRequest>> Set(Guid id, DriverPayRequest request, CancellationToken cancellationToken) =>
        Ok(new DriverPayRequest { Amount = await pay.SetAsync(id, request.Amount, cancellationToken) });

    /// <summary>Every driver with completed rides in that month (Italian time), with their salary.</summary>
    [HttpGet("driver-pay")]
    public async Task<ActionResult<IReadOnlyList<DriverPayLine>>> Month([FromQuery] int year, [FromQuery] int month, CancellationToken cancellationToken)
    {
        if (year is < 2020 or > 2100 || month is < 1 or > 12)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["month"] = ["Pick a month."] }));
        }
        return Ok(await pay.MonthAsync(year, month, cancellationToken));
    }
}
