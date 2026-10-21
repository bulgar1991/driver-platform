using DriverPlatform.Application.Auth;
using DriverPlatform.Application.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = Policies.Admin)]
public sealed class StaffController(IStaffAdminService staff, IAuditQueryService audit) : ControllerBase
{
    [HttpGet("staff")]
    public async Task<ActionResult<IReadOnlyList<StaffDto>>> List(CancellationToken cancellationToken) =>
        Ok(await staff.ListAsync(cancellationToken));

    /// <summary>Creates an account with a temporary password, returned only in this response.</summary>
    [HttpPost("staff")]
    public async Task<ActionResult<TemporaryPasswordResponse>> Create(CreateStaffRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await staff.CreateAsync(request, cancellationToken));

    [HttpPut("staff/{id:guid}")]
    public async Task<ActionResult<StaffDto>> Update(Guid id, UpdateStaffRequest request, CancellationToken cancellationToken) =>
        Ok(await staff.UpdateAsync(id, request, cancellationToken));

    [HttpPost("staff/{id:guid}/reset-password")]
    public async Task<ActionResult<TemporaryPasswordResponse>> ResetPassword(Guid id, CancellationToken cancellationToken) =>
        Ok(await staff.ResetPasswordAsync(id, cancellationToken));

    [HttpGet("audit")]
    public async Task<ActionResult<AuditPage>> Audit([FromQuery] string? search, [FromQuery] long? before, CancellationToken cancellationToken) =>
        Ok(await audit.ListAsync(search, before, cancellationToken));
}
