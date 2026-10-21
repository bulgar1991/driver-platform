using DriverPlatform.Application.Auth;
using DriverPlatform.Application.Payments;
using DriverPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = Policies.Admin)]
public sealed class PaymentsAdminController(IPaymentAdminService payments) : ControllerBase
{
    [HttpGet("payments")]
    public async Task<ActionResult<IReadOnlyList<PaymentListItem>>> List(
        [FromQuery] PaymentStatus? status, [FromQuery] int take = 100, CancellationToken cancellationToken = default) =>
        Ok(await payments.ListAsync(status, take, cancellationToken));

    /// <summary>Refunds through Stripe, issues a credit note and emails it.</summary>
    [HttpPost("payments/{id:guid}/refunds")]
    public async Task<ActionResult<PaymentListItem>> Refund(Guid id, RefundRequest request, CancellationToken cancellationToken) =>
        Ok(await payments.RefundAsync(id, request, cancellationToken));

    [HttpGet("invoices/{id:guid}/pdf")]
    public async Task<IActionResult> InvoicePdf(Guid id, CancellationToken cancellationToken)
    {
        var file = await payments.GetInvoicePdfAsync(id, cancellationToken);
        return File(file.Content, "application/pdf", file.FileName);
    }

    [HttpPost("invoices/{id:guid}/resend")]
    public async Task<IActionResult> Resend(Guid id, CancellationToken cancellationToken)
    {
        await payments.ResendInvoiceAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Invoices and credit notes for the accountant, as CSV.</summary>
    [HttpGet("invoices/export")]
    public async Task<IActionResult> Export([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        var file = await payments.ExportInvoicesAsync(from, to, cancellationToken);
        return File(file.Content, "text/csv; charset=utf-8", file.FileName);
    }
}
