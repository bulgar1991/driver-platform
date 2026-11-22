using System.ComponentModel.DataAnnotations;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Bookings;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Dispatch;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Localization;
using DriverPlatform.Application.Pricing;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Customers;

/// <summary>Which customers the list shows.</summary>
public enum CustomerFilter
{
    All,
    /// <summary>Booked on the website (at least one ride without a partner).</summary>
    Direct,
    /// <summary>Came through a partner, the API or Booking.com.</summary>
    ViaPartner,
    /// <summary>Have a ride still to come.</summary>
    Upcoming,
    /// <summary>More than one ride.</summary>
    Returning,
    /// <summary>Agreed to receive offers.</summary>
    Marketing,
}

public enum CustomerSort
{
    LastRide,
    Name,
    Rides,
    Spent,
}

public sealed record CustomerQuery
{
    [MaxLength(100)]
    public string? Search { get; init; }
    public CustomerFilter Filter { get; init; }
    public CustomerSort Sort { get; init; }
    [Range(1, 10_000)]
    public int Page { get; init; } = 1;
    [Range(1, 200)]
    public int PageSize { get; init; } = 50;
}

public sealed record CustomerListItem(
    Guid Id,
    string Name,
    string? Email,
    string Phone,
    string Language,
    bool MarketingConsent,
    bool HasNotes,
    /// <summary>The partner of the latest partner ride, if any.</summary>
    string? PartnerName,
    int Rides,
    int Cancelled,
    decimal Spent,
    /// <summary>Office-local dates of the last ride done and the next one booked.</summary>
    DateOnly? LastRide,
    DateOnly? NextRide);

public sealed record CustomerSummary(int Customers, int Returning, int Upcoming, decimal Spent);

public sealed record CustomerList(
    IReadOnlyList<CustomerListItem> Items, int Total, int Page, int PageSize, CustomerSummary Summary, string Currency);

public sealed record CustomerRide(
    Guid Id,
    string Reference,
    BookingStatus Status,
    BookingSource Source,
    DateTime PickupLocal,
    string PickupLabel,
    string DropoffLabel,
    VehicleCategory VehicleCategory,
    decimal TotalPrice,
    string? PartnerName,
    string? DriverName);

public sealed record CustomerDetail(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    string Phone,
    string Language,
    bool MarketingConsent,
    DateTimeOffset? GdprConsentAt,
    string? NotesForDriver,
    DateTimeOffset CreatedAt,
    int Rides,
    int Cancelled,
    decimal Spent,
    /// <summary>Average price of the rides not cancelled.</summary>
    decimal? AverageRide,
    IReadOnlyList<string> Partners,
    IReadOnlyList<CustomerRide> History,
    string Currency);

public sealed record CustomerRequest
{
    [Required, MaxLength(100)]
    public required string FirstName { get; init; }
    [MaxLength(100)]
    public string LastName { get; init; } = "";
    [EmailAddress, MaxLength(200)]
    public string? Email { get; init; }
    [Required, RegularExpression(@"^\+?[0-9 ()-]{6,20}$")]
    public required string Phone { get; init; }
    [Required, MaxLength(5)]
    public required string Language { get; init; }
    public bool MarketingConsent { get; init; }
    [MaxLength(1000)]
    public string? NotesForDriver { get; init; }
}

public interface ICustomerService
{
    Task<CustomerList> ListAsync(CustomerQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerListItem>> ExportAsync(CustomerQuery query, CancellationToken cancellationToken);
    Task<CustomerDetail> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<CustomerDetail> UpdateAsync(Guid id, CustomerRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Admin → Customers: the people who booked, with their rides, spend and the notes the drivers see.
/// Checkouts never paid (AwaitingPayment) are not rides: a customer with only those is not listed.
/// </summary>
public sealed class CustomerService(IAppDbContext db, IAuditLogger audit, TimeProvider clock) : ICustomerService
{
    public const int MaxExport = 10_000;
    private const int MaxHistory = 500;

    private sealed class Row
    {
        public Client Client { get; init; } = null!;
        public int Rides { get; init; }
        public int Cancelled { get; init; }
        public decimal Spent { get; init; }
        public DateTimeOffset? Last { get; init; }
        public DateTimeOffset? Next { get; init; }
        public string? Partner { get; init; }
        public bool Direct { get; init; }
    }

    public async Task<CustomerList> ListAsync(CustomerQuery query, CancellationToken cancellationToken)
    {
        var rows = Rows(query);
        var total = await rows.CountAsync(cancellationToken);
        var summary = new CustomerSummary(
            total,
            await rows.CountAsync(r => r.Rides - r.Cancelled > 1, cancellationToken),
            await rows.CountAsync(r => r.Next != null, cancellationToken),
            await rows.SumAsync(r => r.Spent, cancellationToken));
        var page = await Sorted(rows, query.Sort)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return new CustomerList(page.Select(ToItem).ToList(), total, query.Page, query.PageSize, summary, PricingDefaults.Currency);
    }

    public async Task<IReadOnlyList<CustomerListItem>> ExportAsync(CustomerQuery query, CancellationToken cancellationToken) =>
        (await Sorted(Rows(query), query.Sort).Take(MaxExport).ToListAsync(cancellationToken)).Select(ToItem).ToList();

    public async Task<CustomerDetail> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException(Texts.Current.RecordNotFound);
        var bookings = await db.Bookings.AsNoTracking()
            .Where(b => b.ClientId == id && b.Status != BookingStatus.AwaitingPayment)
            .Include(b => b.PickupMeetingPoint)
            .Include(b => b.DropoffMeetingPoint)
            .Include(b => b.Partner)
            .Include(b => b.Assignment).ThenInclude(a => a!.Driver)
            .OrderByDescending(b => b.PickupAt)
            .Take(MaxHistory)
            .ToListAsync(cancellationToken);
        var kept = bookings.Where(b => b.Status != BookingStatus.Cancelled).ToList();
        return new CustomerDetail(
            client.Id, client.FirstName, client.LastName, client.Email, client.Phone, client.Language, client.MarketingConsent,
            client.GdprConsentAt, client.NotesForDriver, client.CreatedAt,
            bookings.Count, bookings.Count - kept.Count, kept.Sum(b => b.TotalPrice),
            kept.Count > 0 ? Math.Round(kept.Average(b => b.TotalPrice), 2) : null,
            bookings.Where(b => b.Partner != null).Select(b => b.Partner!.Name).Distinct().ToList(),
            bookings.Select(b => new CustomerRide(
                b.Id, b.Reference, b.Status, b.Source, RideTime.ToLocal(b.PickupAt, b.Pickup),
                b.PickupMeetingPoint?.Name ?? b.Pickup.Address,
                b.DropoffMeetingPoint?.Name ?? b.Dropoff.Address,
                b.VehicleCategory, b.TotalPrice, b.Partner?.Name,
                b.Assignment == null ? null : $"{b.Assignment.Driver.FirstName} {b.Assignment.Driver.LastName}")).ToList(),
            PricingDefaults.Currency);
    }

    public async Task<CustomerDetail> UpdateAsync(Guid id, CustomerRequest request, CancellationToken cancellationToken)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException(Texts.Current.RecordNotFound);
        var language = request.Language.Trim().ToLowerInvariant();
        if (!SiteLanguages.Codes.Contains(language))
        {
            throw new RequestValidationException("language", Texts.Current.UnknownLanguage);
        }
        client.FirstName = request.FirstName.Trim();
        client.LastName = request.LastName.Trim();
        client.Email = BookingFactory.Normalize(request.Email)?.ToLowerInvariant();
        client.Phone = BookingFactory.NormalizePhone(request.Phone);
        client.Language = language;
        client.MarketingConsent = request.MarketingConsent;
        client.NotesForDriver = BookingFactory.Normalize(request.NotesForDriver);
        // The audit keeps no contact details (personal data), only what kind of change it was.
        audit.Log("client.updated", nameof(Client), id.ToString(), new { request.Language, request.MarketingConsent, HasNotes = client.NotesForDriver != null });
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private IQueryable<Row> Rows(CustomerQuery query)
    {
        var now = clock.GetUtcNow();
        var clients = db.Clients.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            var digits = BookingFactory.NormalizePhone(term);
            clients = clients.Where(c =>
                (c.FirstName + " " + c.LastName).ToLower().Contains(term)
                || (c.Email != null && c.Email.ToLower().Contains(term))
                || (digits.Length >= 3 && c.Phone.Contains(digits))
                || c.Bookings.Any(b => b.Reference.ToLower().Contains(term)));
        }
        var rows = clients
            .Select(c => new
            {
                Client = c,
                Bookings = c.Bookings.Where(b => b.Status != BookingStatus.AwaitingPayment),
            })
            .Select(x => new Row
            {
                Client = x.Client,
                Rides = x.Bookings.Count(),
                Cancelled = x.Bookings.Count(b => b.Status == BookingStatus.Cancelled),
                Spent = x.Bookings.Where(b => b.Status != BookingStatus.Cancelled).Sum(b => (decimal?)b.TotalPrice) ?? 0,
                Last = x.Bookings.Where(b => b.Status != BookingStatus.Cancelled && b.PickupAt <= now).Max(b => (DateTimeOffset?)b.PickupAt),
                Next = x.Bookings.Where(b => b.Status != BookingStatus.Cancelled && b.PickupAt > now).Min(b => (DateTimeOffset?)b.PickupAt),
                Partner = x.Bookings.Where(b => b.PartnerId != null).OrderByDescending(b => b.PickupAt).Select(b => b.Partner!.Name).FirstOrDefault(),
                Direct = x.Bookings.Any(b => b.PartnerId == null),
            })
            .Where(r => r.Rides > 0);
        return query.Filter switch
        {
            CustomerFilter.Direct => rows.Where(r => r.Direct),
            CustomerFilter.ViaPartner => rows.Where(r => r.Partner != null),
            CustomerFilter.Upcoming => rows.Where(r => r.Next != null),
            CustomerFilter.Returning => rows.Where(r => r.Rides - r.Cancelled > 1),
            CustomerFilter.Marketing => rows.Where(r => r.Client.MarketingConsent),
            _ => rows,
        };
    }

    private static IQueryable<Row> Sorted(IQueryable<Row> rows, CustomerSort sort) => sort switch
    {
        CustomerSort.Name => rows.OrderBy(r => r.Client.FirstName).ThenBy(r => r.Client.LastName),
        CustomerSort.Rides => rows.OrderByDescending(r => r.Rides).ThenByDescending(r => r.Spent),
        CustomerSort.Spent => rows.OrderByDescending(r => r.Spent).ThenByDescending(r => r.Rides),
        // Upcoming rides first (the customers the office deals with now), then the latest.
        _ => rows.OrderByDescending(r => r.Next != null).ThenBy(r => r.Next).ThenByDescending(r => r.Last),
    };

    private static CustomerListItem ToItem(Row r) => new(
        r.Client.Id, $"{r.Client.FirstName} {r.Client.LastName}".Trim(), r.Client.Email, r.Client.Phone, r.Client.Language,
        r.Client.MarketingConsent, r.Client.NotesForDriver != null, r.Partner, r.Rides, r.Cancelled, r.Spent,
        r.Last is { } last ? DateOnly.FromDateTime(OfficeTime.ToLocal(last)) : null,
        r.Next is { } next ? DateOnly.FromDateTime(OfficeTime.ToLocal(next)) : null);
}
