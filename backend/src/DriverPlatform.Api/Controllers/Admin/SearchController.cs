using DriverPlatform.Application.Auth;
using DriverPlatform.Application.Search;
using DriverPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Controllers.Admin;

/// <summary>Admin → Search: rides by any filter, and PDF reports. Driver pay is for admins only.</summary>
[ApiController]
[Route("api/admin/search")]
[Authorize(Policy = Policies.Staff)]
public sealed class SearchController(ISearchService search) : ControllerBase
{
    private bool IsAdmin => User.IsInRole(nameof(StaffRole.Admin));

    [HttpGet("options")]
    public async Task<ActionResult<SearchOptions>> Options(CancellationToken cancellationToken) =>
        Ok(await search.OptionsAsync(cancellationToken));

    [HttpGet]
    public async Task<ActionResult<SearchResult>> Search([FromQuery] SearchQuery query, CancellationToken cancellationToken) =>
        Ok(await search.SearchAsync(query, IsAdmin, cancellationToken));

    [HttpGet("report")]
    public async Task<IActionResult> Report([FromQuery] SearchQuery query, [FromQuery] ReportKind kind, CancellationToken cancellationToken)
    {
        var (pdf, fileName) = await search.ReportAsync(query, kind, IsAdmin, cancellationToken);
        return File(pdf, "application/pdf", fileName);
    }
}
