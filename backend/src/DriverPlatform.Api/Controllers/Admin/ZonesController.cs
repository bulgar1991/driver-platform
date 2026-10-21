using DriverPlatform.Application.Admin;
using DriverPlatform.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/zones")]
[Authorize(Policy = Policies.Admin)]
public sealed class ZonesController(IZoneAdminService zones) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ZoneAdminDto>>> List(CancellationToken cancellationToken) =>
        Ok(await zones.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ZoneAdminDto>> Create(ZoneRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await zones.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ZoneAdminDto>> Update(Guid id, ZoneRequest request, CancellationToken cancellationToken) =>
        Ok(await zones.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await zones.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
