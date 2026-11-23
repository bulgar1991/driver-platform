using DriverPlatform.Application.Auth;
using DriverPlatform.Application.Partners;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Controllers.Admin;

/// <summary>Admin → Statements: each partner's month, issued, emailed and marked paid.</summary>
[ApiController]
[Route("api/admin/statements")]
[Authorize(Policy = Policies.Admin)]
public sealed class StatementsController(IPartnerStatementService statements) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<StatementMonth>> Month([FromQuery] int year, [FromQuery] int month, CancellationToken cancellationToken) =>
        Ok(await statements.MonthAsync(year, month, cancellationToken));

    [HttpGet("{partnerId:guid}/{year:int}/{month:int}")]
    public async Task<ActionResult<StatementDetail>> Detail(Guid partnerId, int year, int month, CancellationToken cancellationToken) =>
        Ok(await statements.DetailAsync(partnerId, year, month, cancellationToken));

    [HttpPost("{partnerId:guid}/{year:int}/{month:int}/issue")]
    public async Task<ActionResult<StatementDetail>> Issue(Guid partnerId, int year, int month, CancellationToken cancellationToken) =>
        Ok(await statements.IssueAsync(partnerId, year, month, cancellationToken));

    [HttpPost("{partnerId:guid}/{year:int}/{month:int}/send")]
    public async Task<ActionResult<StatementDetail>> Send(
        Guid partnerId, int year, int month, StatementSendRequest request, CancellationToken cancellationToken) =>
        Ok(await statements.SendAsync(partnerId, year, month, request, cancellationToken));

    [HttpPost("{partnerId:guid}/{year:int}/{month:int}/paid")]
    public async Task<ActionResult<StatementDetail>> Paid(
        Guid partnerId, int year, int month, StatementPaidRequest request, CancellationToken cancellationToken) =>
        Ok(await statements.SetPaidAsync(partnerId, year, month, request, cancellationToken));

    [HttpGet("{partnerId:guid}/{year:int}/{month:int}/pdf")]
    public async Task<IActionResult> Pdf(Guid partnerId, int year, int month, CancellationToken cancellationToken)
    {
        var file = await statements.PdfAsync(partnerId, year, month, cancellationToken);
        return File(file.Content, "application/pdf", file.FileName);
    }

    [HttpGet("{partnerId:guid}/{year:int}/{month:int}/csv")]
    public async Task<IActionResult> Csv(Guid partnerId, int year, int month, CancellationToken cancellationToken)
    {
        var file = await statements.CsvAsync(partnerId, year, month, cancellationToken);
        return File(file.Content, "text/csv; charset=utf-8", file.FileName);
    }
}
