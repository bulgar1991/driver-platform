using DriverPlatform.Application.Auth;
using DriverPlatform.Application.Dispatch;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Controllers.Admin;

/// <summary>A vehicle's deadlines and refuellings. Admins and dispatchers.</summary>
[ApiController]
[Route("api/admin/vehicles/{vehicleId:guid}")]
[Authorize(Policy = Policies.Staff)]
public sealed class VehicleLogController(IVehicleLogService log) : ControllerBase
{
    /// <summary>Refuellings between from and to (default: the last twelve months); all deadlines.</summary>
    [HttpGet("log")]
    public async Task<ActionResult<VehicleLogResponse>> Get(Guid vehicleId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return Ok(await log.GetAsync(vehicleId, from ?? end.AddYears(-1), end, cancellationToken));
    }

    [HttpPost("expirations")]
    public async Task<ActionResult<VehicleExpirationDto>> AddExpiration(Guid vehicleId, VehicleExpirationRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await log.AddExpirationAsync(vehicleId, request, cancellationToken));

    [HttpPut("expirations/{id:guid}")]
    public async Task<ActionResult<VehicleExpirationDto>> UpdateExpiration(Guid vehicleId, Guid id, VehicleExpirationRequest request,
        CancellationToken cancellationToken) =>
        Ok(await log.UpdateExpirationAsync(vehicleId, id, request, cancellationToken));

    [HttpDelete("expirations/{id:guid}")]
    public async Task<IActionResult> DeleteExpiration(Guid vehicleId, Guid id, CancellationToken cancellationToken)
    {
        await log.DeleteExpirationAsync(vehicleId, id, cancellationToken);
        return NoContent();
    }

    [HttpPost("refuels")]
    public async Task<ActionResult<VehicleRefuelDto>> AddRefuel(Guid vehicleId, VehicleRefuelRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await log.AddRefuelAsync(vehicleId, request, cancellationToken));

    [HttpDelete("refuels/{id:guid}")]
    public async Task<IActionResult> DeleteRefuel(Guid vehicleId, Guid id, CancellationToken cancellationToken)
    {
        await log.DeleteRefuelAsync(vehicleId, id, cancellationToken);
        return NoContent();
    }
}
