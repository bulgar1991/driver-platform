using DriverPlatform.Application.Auth;
using DriverPlatform.Application.Messages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Controllers.Admin;

/// <summary>Admin → Messages: what went out to drivers and clients, and sending it again.</summary>
[ApiController]
[Route("api/admin/messages")]
[Authorize(Policy = Policies.Staff)]
public sealed class MessagesController(IMessageLogService messages) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<MessageList>> List([FromQuery] MessageQuery query, CancellationToken cancellationToken) =>
        Ok(await messages.ListAsync(query, cancellationToken));

    [HttpPost("{id:long}/resend")]
    public async Task<IActionResult> Resend(long id, CancellationToken cancellationToken)
    {
        await messages.ResendAsync(id, cancellationToken);
        return NoContent();
    }
}
