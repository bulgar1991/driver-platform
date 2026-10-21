using DriverPlatform.Application.Auth;
using DriverPlatform.Application.Partners;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/partners")]
[Authorize(Policy = Policies.Admin)]
public sealed class PartnersController(IPartnerAdminService partners) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PartnerDto>>> List(CancellationToken cancellationToken) =>
        Ok(await partners.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<PartnerDto>> Create(PartnerRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await partners.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PartnerDto>> Update(Guid id, PartnerRequest request, CancellationToken cancellationToken) =>
        Ok(await partners.UpdateAsync(id, request, cancellationToken));

    /// <summary>Creates a new API key (replacing the old one). The key is shown only in this response.</summary>
    [HttpPost("{id:guid}/api-key")]
    public async Task<ActionResult<PartnerApiKeyResponse>> CreateKey(Guid id, CancellationToken cancellationToken) =>
        Ok(await partners.CreateKeyAsync(id, cancellationToken));

    [HttpDelete("{id:guid}/api-key")]
    public async Task<IActionResult> RevokeKey(Guid id, CancellationToken cancellationToken)
    {
        await partners.RevokeKeyAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/webhook-secret")]
    public async Task<ActionResult<PartnerSecretResponse>> WebhookSecret(Guid id, CancellationToken cancellationToken) =>
        Ok(await partners.WebhookSecretAsync(id, rotate: false, cancellationToken));

    [HttpPost("{id:guid}/webhook-secret")]
    public async Task<ActionResult<PartnerSecretResponse>> RotateWebhookSecret(Guid id, CancellationToken cancellationToken) =>
        Ok(await partners.WebhookSecretAsync(id, rotate: true, cancellationToken));

    [HttpGet("{id:guid}/webhooks")]
    public async Task<ActionResult<IReadOnlyList<WebhookDeliveryDto>>> Deliveries(Guid id, CancellationToken cancellationToken) =>
        Ok(await partners.DeliveriesAsync(id, cancellationToken));

    [HttpGet("{id:guid}/statement")]
    public async Task<IActionResult> Statement(Guid id, [FromQuery] int year, [FromQuery] int month, CancellationToken cancellationToken)
    {
        if (month is < 1 or > 12 || year is < 2020 or > 2100)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["month"] = ["Pick a month."] }));
        }
        var file = await partners.StatementAsync(id, year, month, cancellationToken);
        return File(file.Content, "text/csv; charset=utf-8", file.FileName);
    }
}
