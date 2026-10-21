using DriverPlatform.Application.Reviews;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DriverPlatform.Api.Controllers;

/// <summary>The one-time review link sent after the ride.</summary>
[ApiController]
[Route("api/reviews/{token}")]
[EnableRateLimiting(RateLimitPolicies.Public)]
public sealed class ReviewsController(IReviewService reviews) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ReviewPage>> Get(string token, CancellationToken cancellationToken) =>
        Ok(await reviews.GetAsync(token, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Submit(string token, SubmitReviewRequest request, CancellationToken cancellationToken)
    {
        await reviews.SubmitAsync(token, request, cancellationToken);
        return NoContent();
    }
}
