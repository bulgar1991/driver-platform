using System.Net;
using System.Net.Http.Json;
using DriverPlatform.Application.Bookings;
using DriverPlatform.Application.Dispatch;
using DriverPlatform.Application.Partners;
using DriverPlatform.Application.Payments;
using DriverPlatform.Application.Quotes;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Infrastructure.Persistence;
using DriverPlatform.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DriverPlatform.Tests.Payments;

public sealed class CashPaymentTests
{
    private static OfficeBookingRequest OfficeBooking(PaymentMethod method, decimal? price = null) => new()
    {
        Customer = new OfficeCustomerInput { FirstName = "Luca", LastName = "Bianchi", Phone = "+39 333 1234567", Email = "luca@example.com", Language = "it" },
        // Today, a couple of hours from now: the office has no minimum notice.
        Trip = TestData.Trip() with { PickupAt = DateTime.UtcNow.AddHours(2), NameSign = true },
        VehicleCategory = VehicleCategory.Sedan,
        PassengerName = "Group Bianchi",
        Price = price,
        AdditionalCosts = [new AdditionalCostInput { Description = "Waiting time", Amount = 20 }],
        PaymentMethod = method,
        DriverNotes = "Call on arrival",
        InternalNotes = "Booked by phone",
    };

    [Fact]
    public async Task Office_books_a_ride_paid_in_cash_to_the_driver()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();

        var response = await admin.PostAsJsonAsync("/api/admin/bookings", OfficeBooking(PaymentMethod.CashOnBoard, price: 100), TestData.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<OfficeBookingResult>(TestData.Json))!;

        Assert.Equal(BookingStatus.New, created.Status);
        Assert.Equal(120m, created.Total);
        Assert.Null(created.PaymentUrl);

        var detail = (await admin.GetFromJsonAsync<BookingDetail>($"/api/admin/bookings/{created.Id}", TestData.Json))!;
        Assert.Equal(BookingSource.Office, detail.Source);
        Assert.Equal(PaymentMethod.CashOnBoard, detail.PaymentMethod);
        Assert.Equal(PaymentStatus.Pending, detail.PaymentStatus);
        Assert.True(detail.CollectOnBoard);
        Assert.Equal("Group Bianchi", detail.NameSignText);
        Assert.Equal("Call on arrival", detail.ClientNotes);
        Assert.Equal("Booked by phone", detail.InternalNotes);
        Assert.Equal([100m, 20m], detail.PriceLines.Select(l => l.Amount));

        // The client is told to pay the driver; no invoice yet.
        var email = Assert.Single(factory.Emails.Sent);
        Assert.Equal("luca@example.com", email.To);
        Assert.Contains("120", email.TextBody);
        Assert.Empty(email.Attachments);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.Invoices.ToListAsync());
    }

    [Fact]
    public async Task A_new_customer_is_registered_with_billing_details()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();
        var request = OfficeBooking(PaymentMethod.FollowingInvoice) with
        {
            Customer = new OfficeCustomerInput
            {
                FirstName = "Serghei", LastName = "Rossi", Phone = "+39 333 7654321", Language = "it",
                Company = "Rossi Viaggi Srl", VatNumber = "it01234567890", RecipientCode = "abc1234",
                BillingEmail = "Fatture@Rossi.example", Address = "Via Roma 1", PostalCode = "20121",
                City = "Milano", Province = "MI", Country = "it",
            },
        };

        var response = await admin.PostAsJsonAsync("/api/admin/bookings", request, TestData.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = await db.Clients.SingleAsync(c => c.FirstName == "Serghei");
        Assert.Equal("Rossi Viaggi Srl", client.Company);
        Assert.Equal("IT01234567890", client.VatNumber);
        Assert.Equal("ABC1234", client.RecipientCode);
        Assert.Equal("fatture@rossi.example", client.BillingEmail);
        Assert.Equal(("Via Roma 1", "20121", "Milano", "MI", "IT"), (client.Address, client.PostalCode, client.City, client.Province, client.Country));
    }

    [Fact]
    public async Task Marking_the_cash_received_issues_and_emails_the_invoice()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();
        var created = (await (await admin.PostAsJsonAsync("/api/admin/bookings", OfficeBooking(PaymentMethod.CashOnBoard), TestData.Json))
            .Content.ReadFromJsonAsync<OfficeBookingResult>(TestData.Json))!;
        var detail = (await admin.GetFromJsonAsync<BookingDetail>($"/api/admin/bookings/{created.Id}", TestData.Json))!;

        var received = await admin.PostAsync($"/api/admin/payments/{detail.PaymentId}/received", null);
        var again = await admin.PostAsync($"/api/admin/payments/{detail.PaymentId}/received", null);

        Assert.Equal(HttpStatusCode.OK, received.StatusCode);
        var payment = (await received.Content.ReadFromJsonAsync<PaymentListItem>(TestData.Json))!;
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(created.Total, Assert.Single(payment.Invoices).TotalAmount);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Single(factory.Emails.Sent.Last().Attachments);

        var after = (await admin.GetFromJsonAsync<BookingDetail>($"/api/admin/bookings/{created.Id}", TestData.Json))!;
        Assert.False(after.CollectOnBoard);
    }

    [Fact]
    public async Task Office_booking_paid_online_gets_a_payment_link()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();

        var response = await admin.PostAsJsonAsync("/api/admin/bookings", OfficeBooking(PaymentMethod.Card), TestData.Json);
        var created = (await response.Content.ReadFromJsonAsync<OfficeBookingResult>(TestData.Json))!;

        Assert.Equal(BookingStatus.AwaitingPayment, created.Status);
        Assert.StartsWith("https://magnus.test/dev/payment/", created.PaymentUrl);
        Assert.Empty(factory.Emails.Sent);
    }

    [Fact]
    public async Task Route_start_and_end_are_required()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();
        var request = OfficeBooking(PaymentMethod.CashOnBoard);

        var response = await admin.PostAsJsonAsync("/api/admin/bookings",
            request with { Trip = request.Trip with { Dropoff = new PointInput { Address = "" } } }, TestData.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Website_cash_is_refused_unless_switched_on()
    {
        using (var off = new ApiFactory())
        {
            var response = await off.CreateClient().PostAsJsonAsync("/api/bookings",
                TestData.Booking(TestData.Trip()) with { PayCashOnBoard = true }, TestData.Json);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var on = new ApiFactory { CashOnBoard = true };
        var client = on.CreateClient();
        var options = await client.GetFromJsonAsync<BookingOptionsResponse>("/api/booking-options", TestData.Json);
        var booked = await client.PostAsJsonAsync("/api/bookings",
            TestData.Booking(TestData.Trip()) with { PayCashOnBoard = true }, TestData.Json);
        var result = (await booked.Content.ReadFromJsonAsync<CreateBookingResponse>(TestData.Json))!;

        Assert.True(options!.CashOnBoard);
        Assert.Equal(BookingStatus.New, result.Status);
        Assert.Null(result.PaymentUrl);
        Assert.Contains("cash", Assert.Single(on.Emails.Sent).TextBody);
    }
}
