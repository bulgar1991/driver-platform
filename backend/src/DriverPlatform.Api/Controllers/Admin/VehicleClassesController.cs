using DriverPlatform.Application.Admin;
using DriverPlatform.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Controllers.Admin;

/// <summary>The vehicle classes (tariffs), hidden ones too, for the staff's car and zone forms.</summary>
[ApiController]
[Route("api/admin/vehicle-classes")]
[Authorize(Policy = Policies.Staff)]
public sealed class VehicleClassesController(IPricingAdminService pricing) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VehicleClassDto>>> Get(CancellationToken cancellationToken) =>
        Ok(await pricing.ClassesAsync(cancellationToken));
}
