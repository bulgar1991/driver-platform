using DriverPlatform.Api.Extensions;
using DriverPlatform.Application.Partners;
using DriverPlatform.Application.Quotes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DriverPlatform.Api.Controllers.Partner;

/// <summary>
/// Partner API v1 (business plan §6). Authenticate with the "X-Api-Key" header. Times are Italian
/// local time without offset (pickupAt) plus UTC where noted; amounts are EUR, VAT included.
/// </summary>
[ApiController]
[Route("api/partner/v1")]
[ApiExplorerSettings(GroupName = PartnerApi.Document)]
[Authorize(Policy = PartnerApi.Policy)]
[EnableRateLimiting(RateLimitPolicies.Partner)]
[Produces("application/json")]
public sealed class PartnerBookingsController(IPartnerBookingService bookings) : ControllerBase
{
    private Guid PartnerId => Guid.Parse(User.FindFirst(PartnerApiKeyHandler.PartnerIdClaim)!.Value);

    /// <summary>Price of every vehicle category for a trip, with your agreed rate applied. Nothing is saved.</summary>
    [HttpPost("quotes")]
    public async Task<ActionResult<QuoteResponse>> Quote(QuoteRequest request, CancellationToken cancellationToken) =>
        Ok(await bookings.QuoteAsync(PartnerId, request, cancellationToken));

    /// <summary>
    /// Creates a booking (status "New": no online payment, it is invoiced monthly). 201 when created,
    /// 200 with the existing booking when the externalReference was already used, 409 when the price
    /// changed (the new quote is in "data").
    /// </summary>
    [HttpPost("bookings")]
    [ProducesResponseType<PartnerBooking>(StatusCodes.Status201Created)]
    [ProducesResponseType<PartnerBooking>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PartnerBooking>> Create(PartnerBookingRequest request, CancellationToken cancellationToken)
    {
        var result = await bookings.CreateAsync(PartnerId, request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return result.Created
            ? CreatedAtAction(nameof(Get), new { reference = result.Booking.Reference }, result.Booking)
            : Ok(result.Booking);
    }

    /// <summary>A booking by our reference ("MD-…") or your externalReference. The driver appears once they confirm.</summary>
    [HttpGet("bookings/{reference}")]
    public async Task<ActionResult<PartnerBooking>> Get(string reference, CancellationToken cancellationToken) =>
        Ok(await bookings.GetAsync(PartnerId, reference, cancellationToken));

    /// <summary>Bookings changed after "updatedSince", oldest first, 100 at a time. Pass "nextCursor" back to continue.</summary>
    [HttpGet("bookings")]
    public async Task<ActionResult<PartnerBookingPage>> List([FromQuery] DateTimeOffset? updatedSince, CancellationToken cancellationToken) =>
        Ok(await bookings.ListAsync(PartnerId, updatedSince, cancellationToken));

    /// <summary>Cancels a booking that has not started. Cancelling twice is harmless.</summary>
    [HttpPost("bookings/{reference}/cancel")]
    public async Task<ActionResult<PartnerBooking>> Cancel(string reference, PartnerCancelRequest request, CancellationToken cancellationToken) =>
        Ok(await bookings.CancelAsync(PartnerId, reference, request, cancellationToken));
}
