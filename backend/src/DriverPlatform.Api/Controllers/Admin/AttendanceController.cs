using DriverPlatform.Application.Attendance;
using DriverPlatform.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Controllers.Admin;

/// <summary>Attendance calendar of the staff, and its statistics. Admins and dispatchers; the kinds of event are for admins.</summary>
[ApiController]
[Route("api/admin/attendance")]
[Authorize(Policy = Policies.Staff)]
public sealed class AttendanceController(IAttendanceService attendance) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AttendanceResponse>> List([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await attendance.ListAsync(from, to, cancellationToken));

    /// <summary>Puts the event on every day of the range, replacing what was there.</summary>
    [HttpPut]
    public async Task<ActionResult<IReadOnlyList<AttendanceEntryDto>>> Set(AttendanceRequest request, CancellationToken cancellationToken) =>
        Ok(await attendance.SetAsync(request, cancellationToken));

    [HttpPost("clear")]
    public async Task<ActionResult<int>> Clear(AttendanceClearRequest request, CancellationToken cancellationToken) =>
        Ok(await attendance.ClearAsync(request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await attendance.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("types")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<ActionResult<AttendanceTypeDto>> CreateType(AttendanceTypeRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await attendance.CreateTypeAsync(request, cancellationToken));

    [HttpPut("types/{id:guid}")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<ActionResult<AttendanceTypeDto>> UpdateType(Guid id, AttendanceTypeRequest request, CancellationToken cancellationToken) =>
        Ok(await attendance.UpdateTypeAsync(id, request, cancellationToken));

    [HttpDelete("types/{id:guid}")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<IActionResult> DeleteType(Guid id, CancellationToken cancellationToken)
    {
        await attendance.DeleteTypeAsync(id, cancellationToken);
        return NoContent();
    }
}
