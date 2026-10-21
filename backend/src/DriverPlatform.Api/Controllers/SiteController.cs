using DriverPlatform.Application.DriverPortal;
using DriverPlatform.Application.Reviews;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DriverPlatform.Api.Controllers;

/// <summary>Content of the public home page: testimonials and the driver application form.</summary>
[ApiController]
[Route("api")]
public sealed class SiteController(IReviewService reviews, IDriverApplicationService applications) : ControllerBase
{
    [HttpGet("testimonials")]
    [EnableRateLimiting(RateLimitPolicies.Public)]
    public async Task<ActionResult<IReadOnlyList<Testimonial>>> Testimonials(CancellationToken cancellationToken)
    {
        // Public content changes rarely: let the browser and Caddy keep it 5 minutes.
        Response.Headers.CacheControl = "public, max-age=300";
        return Ok(await reviews.TestimonialsAsync(cancellationToken));
    }

    [HttpPost("driver-applications")]
    [EnableRateLimiting(RateLimitPolicies.Form)]
    public async Task<IActionResult> Apply(DriverApplicationRequest request, CancellationToken cancellationToken)
    {
        await applications.SubmitAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Accepted();
    }
}
