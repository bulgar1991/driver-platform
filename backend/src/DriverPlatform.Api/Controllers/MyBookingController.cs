using DriverPlatform.Application.MyBooking;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DriverPlatform.Api.Controllers;

/// <summary>The client's booking page, opened from the signed link in the confirmation.</summary>
[ApiController]
[Route("api/my-booking/{reference}/{token}")]
[EnableRateLimiting(RateLimitPolicies.Public)]
public sealed class MyBookingController(IMyBookingService myBooking) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<MyBookingResponse>> Get(string reference, string token, CancellationToken cancellationToken) =>
        Ok(await myBooking.GetAsync(reference, token, cancellationToken));

    /// <summary>Opens a new payment page if the booking is still unpaid.</summary>
    [HttpPost("checkout")]
    public async Task<ActionResult<CheckoutUrlResponse>> Checkout(string reference, string token, CancellationToken cancellationToken) =>
        Ok(new CheckoutUrlResponse(await myBooking.StartCheckoutAsync(reference, token, cancellationToken)));

    [HttpGet("invoices/{number}")]
    public async Task<IActionResult> Invoice(string reference, string token, string number, CancellationToken cancellationToken)
    {
        var file = await myBooking.GetInvoicePdfAsync(reference, token, number, cancellationToken);
        return File(file.Content, "application/pdf", file.FileName);
    }
}

public sealed record CheckoutUrlResponse(string Url);
