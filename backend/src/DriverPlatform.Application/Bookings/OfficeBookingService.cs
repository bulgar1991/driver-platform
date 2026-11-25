using System.ComponentModel.DataAnnotations;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Localization;
using DriverPlatform.Application.Partners;
using DriverPlatform.Application.Payments;
using DriverPlatform.Application.Quotes;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DriverPlatform.Application.Bookings;

public sealed record AdditionalCostInput
{
    [Required, MaxLength(100)]
    public required string Description { get; init; }
    [Range(-100_000, 100_000)]
    public decimal Amount { get; init; }
}

/// <summary>A new customer registered with the booking (the "Customer record" popup).</summary>
public sealed record OfficeCustomerInput
{
    [Required, MaxLength(100)]
    public required string FirstName { get; init; }
    [MaxLength(100)]
    public string LastName { get; init; } = "";
    [Required, Phone, MaxLength(32)]
    public required string Phone { get; init; }
    [EmailAddress, MaxLength(254)]
    public string? Email { get; init; }
    [RegularExpression("^[a-z]{2}$")]
    public string Language { get; init; } = "en";
    [MaxLength(150)]
    public string? Company { get; init; }
    [MaxLength(32)]
    public string? TaxCode { get; init; }
    [MaxLength(32)]
    public string? VatNumber { get; init; }
    [RegularExpression("^[A-Za-z0-9]{6,7}$")]
    public string? RecipientCode { get; init; }
    [EmailAddress, MaxLength(254)]
    public string? PecEmail { get; init; }
    [EmailAddress, MaxLength(254)]
    public string? BillingEmail { get; init; }
    [MaxLength(200)]
    public string? Address { get; init; }
    [MaxLength(16)]
    public string? PostalCode { get; init; }
    [MaxLength(100)]
    public string? City { get; init; }
    [MaxLength(50)]
    public string? Province { get; init; }
    [RegularExpression("^[A-Za-z]{2}$")]
    public string? Country { get; init; }
    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed record OfficeBookingRequest
{
    /// <summary>An existing customer; otherwise <see cref="Customer"/> is found by phone (and email) or created.</summary>
    public Guid? ClientId { get; init; }
    public OfficeCustomerInput? Customer { get; init; }
    /// <summary>One way: a return is entered as its own booking. Route start and end are required.</summary>
    [Required]
    public required QuoteRequest Trip { get; init; }
    public VehicleCategory VehicleCategory { get; init; }
    /// <summary>Business relation: the partner the ride is for, if any.</summary>
    public Guid? PartnerId { get; init; }
    /// <summary>The partner's or the client's own booking id.</summary>
    [MaxLength(64)]
    public string? ExternalReference { get; init; }
    /// <summary>Passenger or group name: on the driver's sign when <c>Trip.NameSign</c>, otherwise in the driver's notes.</summary>
    [MaxLength(60)]
    public string? PassengerName { get; init; }
    [MaxLength(16), RegularExpression("^[A-Za-z0-9 ]{2,16}$")]
    public string? FlightNumber { get; init; }
    /// <summary>The ride's price, VAT included, instead of the tariff; empty keeps the tariff price.</summary>
    [Range(0, 100_000)]
    public decimal? Price { get; init; }
    [MaxLength(10)]
    public List<AdditionalCostInput> AdditionalCosts { get; init; } = [];
    public PaymentMethod PaymentMethod { get; init; }
    [MaxLength(2000)]
    public string? DriverNotes { get; init; }
    [MaxLength(2000)]
    public string? InternalNotes { get; init; }
}

/// <param name="PaymentUrl">Stripe's payment page to send the client, when paid online.</param>
public sealed record OfficeBookingResult(Guid Id, string Reference, BookingStatus Status, decimal Total, string? PaymentUrl);

public interface IOfficeBookingService
{
    Task<QuoteResponse> QuoteAsync(QuoteRequest request, CancellationToken cancellationToken);
    Task<OfficeBookingResult> CreateAsync(OfficeBookingRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Admin → Bookings → New booking: a ride taken by phone, WhatsApp or email. The price comes from the tariffs
/// and the office may change it; the payment is online (a Stripe link for the client) or recorded by hand.
/// </summary>
public sealed class OfficeBookingService(
    IAppDbContext db,
    IQuoteService quotes,
    IPaymentService payments,
    IAuditLogger audit,
    ILogger<OfficeBookingService> logger) : IOfficeBookingService
{
    public Task<QuoteResponse> QuoteAsync(QuoteRequest request, CancellationToken cancellationToken) =>
        quotes.QuoteForStaffAsync(request with { Return = null }, cancellationToken);

    public async Task<OfficeBookingResult> CreateAsync(OfficeBookingRequest request, CancellationToken cancellationToken)
    {
        if (request.PaymentMethod == PaymentMethod.PayPal)
        {
            throw new RequestValidationException("paymentMethod", Texts.Current.CashNotOffered);
        }
        var trip = request.Trip with { Return = null };
        var quote = await quotes.QuoteForStaffAsync(trip, cancellationToken);
        // The office may book a car that does not fit by the rules (it knows the client); only the tariff has to exist.
        var vehicle = quote.Vehicles.FirstOrDefault(v => v.Category == request.VehicleCategory)
            ?? throw new RequestValidationException("vehicleCategory", Texts.Current.CategoryNotOffered);

        Partner? partner = null;
        if (request.PartnerId is { } partnerId)
        {
            partner = await db.Partners.FirstOrDefaultAsync(p => p.Id == partnerId, cancellationToken)
                ?? throw new NotFoundException(Texts.Current.PartnerNotFound);
        }
        var client = await ClientAsync(request, cancellationToken);

        // Without a name sign, the passenger or group name goes to the driver with the notes.
        var passenger = BookingFactory.Normalize(request.PassengerName);
        var notes = trip.NameSign || passenger is null
            ? request.DriverNotes
            : string.Join("\n", new[] { passenger, BookingFactory.Normalize(request.DriverNotes) }.Where(s => s is not null));
        var extras = new BookingExtras(request.FlightNumber, trip.NameSign, passenger, notes);
        var online = request.PaymentMethod == PaymentMethod.Card;
        var booking = BookingFactory.Create(client, quote.Outbound, vehicle.Outbound, trip, request.VehicleCategory,
            extras, quote.VatRatePercent, BookingSource.Office, online ? BookingStatus.AwaitingPayment : BookingStatus.New, null);
        booking.Reference = await db.NewReferenceAsync(cancellationToken);
        booking.PartnerId = partner?.Id;
        booking.ExternalReference = BookingFactory.Normalize(request.ExternalReference);
        booking.InternalNotes = BookingFactory.Normalize(request.InternalNotes);
        ApplyPrice(booking, request, Texts.For(client.Language));
        db.Bookings.Add(booking);

        audit.Log("booking.created_by_office", nameof(Booking), booking.Id.ToString(),
            new { booking.Reference, booking.TotalPrice, request.PaymentMethod, PriceChanged = request.Price is not null });
        await db.SaveChangesAsync(cancellationToken);

        string? paymentUrl = null;
        if (online)
        {
            try
            {
                paymentUrl = await payments.StartCheckoutAsync(booking, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The booking stays; a new link can be opened from the client's booking page.
                logger.LogError(ex, "Could not open the payment page for {Reference}", booking.Reference);
            }
        }
        else
        {
            await payments.RecordManualAsync(booking, request.PaymentMethod, cancellationToken);
        }
        return new OfficeBookingResult(booking.Id, booking.Reference, booking.Status, booking.TotalPrice, paymentUrl);
    }

    /// <summary>The office's own price replaces the tariff lines; additional costs are added as lines of their own.</summary>
    private static void ApplyPrice(Booking booking, OfficeBookingRequest request, Texts texts)
    {
        if (request.Price is { } price)
        {
            booking.PriceLines = [new PriceLine { Kind = PriceLineKind.Fare, Label = texts.FareLine(booking.VehicleCategory, booking.DistanceKm), Amount = price }];
            booking.BaseFare = price;
            booking.SurchargePercent = 0;
            booking.SurchargeTotal = 0;
            booking.DiscountTotal = 0;
            booking.ZoneFeesTotal = 0;
            booking.ExtrasTotal = 0;
        }
        foreach (var cost in request.AdditionalCosts.Where(c => c.Amount != 0))
        {
            booking.PriceLines.Add(new PriceLine { Kind = PriceLineKind.Extra, Label = cost.Description.Trim(), Amount = cost.Amount });
            booking.ExtrasTotal += cost.Amount;
        }
        booking.TotalPrice = booking.PriceLines.Sum(l => l.Amount);
        if (booking.TotalPrice < 0)
        {
            throw new RequestValidationException("price", Texts.Current.PriceBelowZero);
        }
    }

    private async Task<Client> ClientAsync(OfficeBookingRequest request, CancellationToken cancellationToken)
    {
        if (request.ClientId is { } id)
        {
            return await db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                ?? throw new NotFoundException(Texts.Current.CustomerNotFound);
        }
        var customer = request.Customer ?? throw new RequestValidationException("customer", Texts.Current.CustomerRequired);
        var phone = BookingFactory.NormalizePhone(customer.Phone);
        var email = BookingFactory.Normalize(customer.Email)?.ToLowerInvariant();
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Phone == phone && (email == null || c.Email == email), cancellationToken);
        if (client is null)
        {
            client = new Client
            {
                FirstName = customer.FirstName.Trim(),
                LastName = customer.LastName.Trim(),
                Phone = phone,
                Email = email,
                Language = customer.Language,
            };
            db.Clients.Add(client);
        }
        // Billing details typed in the customer record fill what the customer does not have yet.
        var n = BookingFactory.Normalize;
        client.Company ??= n(customer.Company);
        client.TaxCode ??= n(customer.TaxCode)?.ToUpperInvariant();
        client.VatNumber ??= n(customer.VatNumber)?.ToUpperInvariant();
        client.RecipientCode ??= n(customer.RecipientCode)?.ToUpperInvariant();
        client.PecEmail ??= n(customer.PecEmail)?.ToLowerInvariant();
        client.BillingEmail ??= n(customer.BillingEmail)?.ToLowerInvariant();
        client.Address ??= n(customer.Address);
        client.PostalCode ??= n(customer.PostalCode);
        client.City ??= n(customer.City);
        client.Province ??= n(customer.Province);
        client.Country ??= n(customer.Country)?.ToUpperInvariant();
        client.OfficeNotes ??= n(customer.Notes);
        return client;
    }
}
