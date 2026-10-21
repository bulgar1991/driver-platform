using DriverPlatform.Application.Payments;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(IPaymentService payments) : ControllerBase
{
    /// <summary>Stripe webhook. Configure it for checkout.session.completed, .async_payment_succeeded, .async_payment_failed and .expired.</summary>
    [HttpPost("stripe/webhook")]
    public async Task<IActionResult> StripeWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        await payments.HandleWebhookAsync(payload, Request.Headers["Stripe-Signature"].ToString(), cancellationToken);
        return Ok();
    }
}
