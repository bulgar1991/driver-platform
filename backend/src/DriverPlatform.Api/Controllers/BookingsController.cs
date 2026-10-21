using DriverPlatform.Application.Bookings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DriverPlatform.Api.Controllers;

[ApiController]
[Route("api/bookings")]
public sealed class BookingsController(IBookingService bookings) : ControllerBase
{
    /// <summary>Creates a website booking awaiting payment. 409 means the price changed; the new quote is in "data".</summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Booking)]
    public async Task<ActionResult<CreateBookingResponse>> Create(CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await bookings.CreateAsync(request, ip, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
