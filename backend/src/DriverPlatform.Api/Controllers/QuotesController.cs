using DriverPlatform.Application.Quotes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DriverPlatform.Api.Controllers;

[ApiController]
[Route("api")]
[EnableRateLimiting(RateLimitPolicies.Public)]
public sealed class QuotesController(IQuoteService quotes) : ControllerBase
{
    /// <summary>Vehicle capacities and extra fees for the booking form.</summary>
    [HttpGet("booking-options")]
    public async Task<ActionResult<BookingOptionsResponse>> Options(CancellationToken cancellationToken) =>
        Ok(await quotes.GetOptionsAsync(cancellationToken));

    /// <summary>Route, zones and the price of every vehicle category. Nothing is saved.</summary>
    [HttpPost("quotes")]
    public async Task<ActionResult<QuoteResponse>> Quote(QuoteRequest request, CancellationToken cancellationToken) =>
        Ok(await quotes.QuoteAsync(request, cancellationToken));
}
