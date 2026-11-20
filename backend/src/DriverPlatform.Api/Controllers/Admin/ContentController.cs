using DriverPlatform.Application.Auth;
using DriverPlatform.Application.Content;
using DriverPlatform.Application.Dispatch;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Controllers.Admin;

/// <summary>Admin → Website content: texts and pictures of the public site.</summary>
[ApiController]
[Route("api/admin/content")]
[Authorize(Policy = Policies.Admin)]
public sealed class ContentController(ISiteContentService content) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SiteContentDto>> Get(CancellationToken cancellationToken) =>
        Ok(await content.GetAsync(cancellationToken));

    /// <summary>Empty languages go back to the site's own text.</summary>
    [HttpPut("texts/{key}")]
    public async Task<ActionResult<SiteContentDto>> SetText(string key, ContentText text, CancellationToken cancellationToken) =>
        Ok(await content.SetTextAsync(key, text, cancellationToken));

    [HttpPut("images/{key}")]
    [RequestSizeLimit(Photos.MaxBytes + 64 * 1024)]
    public async Task<ActionResult<SiteContentDto>> SetImage(string key, IFormFile file, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream((int)Math.Min(file.Length, Photos.MaxBytes + 1));
        await file.CopyToAsync(buffer, cancellationToken);
        return Ok(await content.SetImageAsync(key, buffer.ToArray(), cancellationToken));
    }

    [HttpDelete("images/{key}")]
    public async Task<ActionResult<SiteContentDto>> RemoveImage(string key, CancellationToken cancellationToken) =>
        Ok(await content.RemoveImageAsync(key, cancellationToken));
}
