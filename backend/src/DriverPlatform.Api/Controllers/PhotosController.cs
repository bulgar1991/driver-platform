using DriverPlatform.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Api.Controllers;

/// <summary>
/// Vehicle and driver pictures. Public so that &lt;img&gt; tags work without a token; the ids are random and
/// only shown in the staff panel. Not rate limited: a fleet list loads one per row.
/// </summary>
[ApiController]
[Route("api/photos")]
public sealed class PhotosController(IAppDbContext db) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var photo = await db.Photos.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new { p.ContentType, p.Data })
            .FirstOrDefaultAsync(cancellationToken);
        if (photo is null)
        {
            return NotFound();
        }
        // A photo never changes: a new upload gets a new id.
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return File(photo.Data, photo.ContentType);
    }
}
