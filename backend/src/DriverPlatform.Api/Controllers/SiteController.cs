using DriverPlatform.Application.Content;
using DriverPlatform.Application.Visitors;
using DriverPlatform.Application.DriverPortal;
using DriverPlatform.Application.Reviews;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DriverPlatform.Api.Controllers;

/// <summary>Content of the public home page: website texts and pictures, testimonials, the driver application form.</summary>
[ApiController]
[Route("api")]
public sealed class SiteController(
    IReviewService reviews, IDriverApplicationService applications, ISiteContentService content, IVisitorService visitors) : ControllerBase
{
    /// <summary>Texts and pictures changed in Admin → Website content (the rest is built into the site).</summary>
    [HttpGet("content")]
    [EnableRateLimiting(RateLimitPolicies.Public)]
    public async Task<ActionResult<SiteContentDto>> Content(CancellationToken cancellationToken)
    {
        // Short, so an edit shows within a minute.
        Response.Headers.CacheControl = "public, max-age=60";
        return Ok(await content.GetAsync(cancellationToken));
    }

    [HttpGet("testimonials")]
    [EnableRateLimiting(RateLimitPolicies.Public)]
    public async Task<ActionResult<IReadOnlyList<Testimonial>>> Testimonials(CancellationToken cancellationToken)
    {
        // Public content changes rarely: let the browser and Caddy keep it 5 minutes.
        Response.Headers.CacheControl = "public, max-age=300";
        return Ok(await reviews.TestimonialsAsync(cancellationToken));
    }

    /// <summary>A page seen on the public site (Admin → Visitors). Pages with a private link are ignored.</summary>
    [HttpPost("visits")]
    [EnableRateLimiting(RateLimitPolicies.Public)]
    public async Task<IActionResult> Visit(VisitRequest request, CancellationToken cancellationToken)
    {
        await visitors.RecordAsync(request, HttpContext.Connection.RemoteIpAddress,
            Request.Headers.UserAgent.ToString(), Request.Host.Host, cancellationToken);
        return NoContent();
    }

    [HttpPost("driver-applications")]
    [EnableRateLimiting(RateLimitPolicies.Form)]
    public async Task<IActionResult> Apply(DriverApplicationRequest request, CancellationToken cancellationToken)
    {
        await applications.SubmitAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Accepted();
    }
}
