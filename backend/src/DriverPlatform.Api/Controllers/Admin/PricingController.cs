using DriverPlatform.Application.Admin;
using DriverPlatform.Application.Auth;
using DriverPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/pricing")]
[Authorize(Policy = Policies.Admin)]
public sealed class PricingController(IPricingAdminService pricing) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PricingOverview>> Get(CancellationToken cancellationToken) =>
        Ok(await pricing.GetAsync(cancellationToken));

    [HttpPut("rates/{category}")]
    public async Task<ActionResult<VehicleRateDto>> UpdateRate(
        VehicleCategory category, UpdateVehicleRateRequest request, CancellationToken cancellationToken) =>
        Ok(await pricing.UpdateRateAsync(category, request, cancellationToken));

    [HttpPut("extras/{type}")]
    public async Task<ActionResult<ExtraFeeDto>> UpdateExtra(
        ExtraType type, UpdateExtraFeeRequest request, CancellationToken cancellationToken) =>
        Ok(await pricing.UpdateExtraAsync(type, request, cancellationToken));

    [HttpPut("settings")]
    public async Task<ActionResult<PricingSettingsDto>> UpdateSettings(
        PricingSettingsDto request, CancellationToken cancellationToken) =>
        Ok(await pricing.UpdateSettingsAsync(request, cancellationToken));

    [HttpPost("surcharges")]
    public async Task<ActionResult<SurchargeRuleDto>> CreateSurcharge(
        SurchargeRuleRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await pricing.CreateSurchargeAsync(request, cancellationToken));

    [HttpPut("surcharges/{id:guid}")]
    public async Task<ActionResult<SurchargeRuleDto>> UpdateSurcharge(
        Guid id, SurchargeRuleRequest request, CancellationToken cancellationToken) =>
        Ok(await pricing.UpdateSurchargeAsync(id, request, cancellationToken));

    [HttpDelete("surcharges/{id:guid}")]
    public async Task<IActionResult> DeleteSurcharge(Guid id, CancellationToken cancellationToken)
    {
        await pricing.DeleteSurchargeAsync(id, cancellationToken);
        return NoContent();
    }
}
