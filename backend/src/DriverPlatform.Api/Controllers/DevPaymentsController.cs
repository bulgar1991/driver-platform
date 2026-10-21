using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Payments;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Infrastructure.Payments;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Controllers;

/// <summary>
/// The fake payment page's API (Payments:Provider = "Fake"). Answers 404 when Stripe is configured,
/// so it can never mark a real booking as paid.
/// </summary>
[ApiController]
[Route("api/dev/payments/{sessionId}")]
public sealed class DevPaymentsController(IPaymentGateway gateway, IPaymentService payments) : ControllerBase
{
    public sealed record DevSession(string Reference, decimal Amount, string Currency, IReadOnlyList<CheckoutLine> Lines);

    public sealed record DevRedirect(string Url);

    public sealed record CompleteRequest(PaymentMethod Method);

    [HttpGet]
    public ActionResult<DevSession> Get(string sessionId)
    {
        if (gateway is not FakePaymentGateway fake)
        {
            return NotFound();
        }
        var session = fake.Find(sessionId);
        return new DevSession(session.Request.Reference, session.Amount, session.Request.Currency, session.Request.Lines);
    }

    [HttpPost("complete")]
    public async Task<ActionResult<DevRedirect>> Complete(string sessionId, CompleteRequest request, CancellationToken cancellationToken)
    {
        if (gateway is not FakePaymentGateway fake)
        {
            return NotFound();
        }
        fake.MarkPaid(sessionId, request.Method);
        // What Stripe's webhook would do.
        await payments.HandleEventAsync(new PaymentEvent($"fake_evt_{Guid.NewGuid():N}", PaymentEventType.CheckoutCompleted, sessionId), cancellationToken);
        return new DevRedirect(fake.Find(sessionId).Request.SuccessUrl);
    }

    [HttpPost("cancel")]
    public ActionResult<DevRedirect> Cancel(string sessionId) =>
        gateway is FakePaymentGateway fake ? new DevRedirect(fake.Find(sessionId).Request.CancelUrl) : NotFound();
}
