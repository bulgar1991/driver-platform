using System.Net;
using System.Net.Http.Json;
using System.Text;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Bookings;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.MyBooking;
using DriverPlatform.Application.Payments;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Infrastructure.Persistence;
using DriverPlatform.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DriverPlatform.Tests.Payments;

public sealed class PaymentFlowTests
{
    private static async Task<CreateBookingResponse> BookAsync(HttpClient client, Application.Quotes.QuoteRequest? trip = null)
    {
        var response = await client.PostAsJsonAsync("/api/bookings", TestData.Booking(trip ?? TestData.Trip()), TestData.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateBookingResponse>(TestData.Json))!;
    }

    private static string SessionId(CreateBookingResponse booking) => booking.PaymentUrl!.Split('/').Last();

    /// <summary>"https://magnus.test/my-booking/MD-XXXXXX/token" → "/api/my-booking/MD-XXXXXX/token".</summary>
    private static string MyBookingApi(CreateBookingResponse booking) =>
        "/api" + new Uri(booking.ManageUrl).AbsolutePath;

    private static async Task PayAsync(HttpClient client, CreateBookingResponse booking, PaymentMethod method = PaymentMethod.Card)
    {
        var response = await client.PostAsJsonAsync($"/api/dev/payments/{SessionId(booking)}/complete", new { method }, TestData.Json);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Booking_opens_a_payment_page_and_a_signed_client_link()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var booking = await BookAsync(client);
        var page = await client.GetFromJsonAsync<MyBookingResponse>(MyBookingApi(booking), TestData.Json);
        var forged = await client.GetAsync($"/api/my-booking/{booking.Reference}/not-the-token");

        Assert.StartsWith("https://magnus.test/dev/payment/fake_cs_", booking.PaymentUrl);
        Assert.StartsWith($"https://magnus.test/my-booking/{booking.Reference}/", booking.ManageUrl);
        Assert.Equal(BookingStatus.AwaitingPayment, page!.Status);
        Assert.True(page.CanPay);
        Assert.Equal(PaymentStatus.Pending, page.Payment?.Status);
        Assert.Equal(HttpStatusCode.NotFound, forged.StatusCode);
    }

    [Fact]
    public async Task Paid_booking_becomes_new_gets_an_invoice_and_one_confirmation_email()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var booking = await BookAsync(client);

        await PayAsync(client, booking, PaymentMethod.PayPal);
        // Stripe retries webhooks: a second delivery must change nothing.
        await PayAsync(client, booking, PaymentMethod.PayPal);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.Bookings.SingleAsync();
        var payment = await db.Payments.SingleAsync();
        var invoice = await db.Invoices.SingleAsync();
        Assert.Equal(BookingStatus.New, saved.Status);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(PaymentMethod.PayPal, payment.Method);
        Assert.Equal(saved.TotalPrice, payment.Amount);
        Assert.Equal($"MD-{ItalyTime.ToLocal(DateTimeOffset.UtcNow).Year}-000001", invoice.Number);
        Assert.Equal(saved.TotalPrice, invoice.TotalAmount);
        Assert.Equal(invoice.TotalAmount, invoice.NetAmount + invoice.VatAmount);
        Assert.Equal("Maria Rossi", invoice.Buyer.Name);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(invoice.Pdf!, 0, 4));
        Assert.Equal(InvoiceStatus.Sent, invoice.Status);

        var email = Assert.Single(factory.Emails.Sent);
        Assert.Equal("maria@example.com", email.To);
        Assert.Contains(booking.Reference, email.Subject);
        Assert.Contains(booking.ManageUrl, email.TextBody);
        Assert.Equal($"Invoice-{invoice.Number}.pdf", Assert.Single(email.Attachments).FileName);

        var page = await client.GetFromJsonAsync<MyBookingResponse>(MyBookingApi(booking), TestData.Json);
        Assert.False(page!.CanPay);
        Assert.Equal(invoice.Number, Assert.Single(page.Invoices).Number);
        var pdf = await client.GetAsync($"{MyBookingApi(booking)}/invoices/{invoice.Number}");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);

        var again = await client.PostAsync($"{MyBookingApi(booking)}/checkout", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Return_trip_is_paid_and_invoiced_together()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var trip = TestData.Trip() with { Return = new() { PickupAt = TestData.QuietPickup().AddDays(7) } };
        var booking = await BookAsync(client, trip);

        await PayAsync(client, booking);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var legs = await db.Bookings.ToListAsync();
        var invoice = await db.Invoices.SingleAsync();
        Assert.All(legs, b => Assert.Equal(BookingStatus.New, b.Status));
        Assert.Equal(legs.Sum(b => b.TotalPrice), (await db.Payments.SingleAsync()).Amount);
        Assert.Equal(legs.Sum(b => b.TotalPrice), invoice.TotalAmount);
        Assert.All(legs, b => Assert.Contains(invoice.Lines, l => l.Description.Contains(b.Reference)));
    }

    [Fact]
    public async Task Invoice_numbers_run_without_gaps()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var first = await BookAsync(client);
        var second = await BookAsync(client);

        await PayAsync(client, second);
        await PayAsync(client, first);

        using var scope = factory.Services.CreateScope();
        var numbers = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Invoices
            .OrderBy(i => i.Sequence).Select(i => i.Sequence).ToListAsync();
        Assert.Equal([1, 2], numbers);
    }

    [Fact]
    public async Task Expired_checkout_keeps_the_booking_payable()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var booking = await BookAsync(client);

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IPaymentService>().HandleEventAsync(
                new PaymentEvent("evt_1", PaymentEventType.CheckoutExpired, SessionId(booking)), CancellationToken.None);
        }
        var page = await client.GetFromJsonAsync<MyBookingResponse>(MyBookingApi(booking), TestData.Json);
        var retry = await client.PostAsync($"{MyBookingApi(booking)}/checkout", null);

        Assert.Equal(PaymentStatus.Failed, page!.Payment?.Status);
        Assert.True(page.CanPay);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    [Fact]
    public async Task Refunds_issue_credit_notes_and_a_full_refund_can_cancel_the_ride()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var booking = await BookAsync(client);
        await PayAsync(client, booking);
        var admin = await factory.CreateAdminClientAsync();
        var payment = Assert.Single((await admin.GetFromJsonAsync<List<PaymentListItem>>("/api/admin/payments", TestData.Json))!);

        var partial = await admin.PostAsJsonAsync($"/api/admin/payments/{payment.Id}/refunds",
            new RefundRequest { Amount = 10m, Reason = "Late pickup" }, TestData.Json);
        var tooMuch = await admin.PostAsJsonAsync($"/api/admin/payments/{payment.Id}/refunds",
            new RefundRequest { Amount = payment.Amount, Reason = "Too much" }, TestData.Json);
        var rest = await admin.PostAsJsonAsync($"/api/admin/payments/{payment.Id}/refunds",
            new RefundRequest { Reason = "Client cancelled", CancelBooking = true }, TestData.Json);

        Assert.Equal(PaymentStatus.PartiallyRefunded, (await partial.Content.ReadFromJsonAsync<PaymentListItem>(TestData.Json))!.Status);
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        var final = (await rest.Content.ReadFromJsonAsync<PaymentListItem>(TestData.Json))!;
        Assert.Equal(PaymentStatus.Refunded, final.Status);
        Assert.Equal(payment.Amount, final.RefundedAmount);
        Assert.Equal(BookingStatus.Cancelled, final.BookingStatus);

        var creditNotes = final.Invoices.Where(i => i.Type == InvoiceType.CreditNote).ToList();
        Assert.Equal(2, creditNotes.Count);
        Assert.Equal(10m, creditNotes[0].TotalAmount);
        Assert.Equal(payment.Amount - 10m, creditNotes[1].TotalAmount);
        Assert.StartsWith("NC-", creditNotes[0].Number);
        Assert.Equal(3, factory.Emails.Sent.Count); // confirmation + two refunds

        var today = DateOnly.FromDateTime(ItalyTime.ToLocal(DateTimeOffset.UtcNow));
        var csv = await admin.GetStringAsync($"/api/admin/invoices/export?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
        Assert.Equal(4, csv.Trim().Split('\n').Length); // header, invoice, two credit notes
        Assert.Contains(";-10,00;", csv);

        using var scope = factory.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.AnyAsync(a => a.Action == "payment.refunded"));
    }

    [Fact]
    public async Task Payments_admin_requires_a_signed_in_admin()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateClient().GetAsync("/api/admin/payments");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
