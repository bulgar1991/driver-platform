using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Localization;
using DriverPlatform.Application.Pricing;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Search;

public interface ISearchService
{
    Task<SearchOptions> OptionsAsync(CancellationToken cancellationToken);
    Task<SearchResult> SearchAsync(SearchQuery query, bool includePay, CancellationToken cancellationToken);
    /// <summary>The PDF of a report; refused when the filters do not allow that kind.</summary>
    Task<(byte[] Pdf, string FileName)> ReportAsync(SearchQuery query, ReportKind kind, bool includePay, CancellationToken cancellationToken);
}

/// <summary>
/// Admin → Search: rides by any combination of filters, with totals, an Excel export (in the browser) and
/// PDF reports. Which reports are offered follows the filters: a driver report needs a driver, and so on.
/// </summary>
public sealed class SearchService(IAppDbContext db, IReportRenderer renderer, TimeProvider clock) : ISearchService
{
    public const int MaxDays = 366;
    public const int MaxRows = 2000;

    public static IReadOnlyList<ReportKind> AvailableReports(SearchQuery query)
    {
        var kinds = new List<ReportKind> { ReportKind.List };
        if (query.From == query.To)
        {
            kinds.Add(ReportKind.DailySheet);
        }
        if (query.DriverId is not null)
        {
            kinds.Add(ReportKind.Driver);
        }
        if (query.PartnerId is not null)
        {
            kinds.Add(ReportKind.Partner);
        }
        if (query.VehicleId is not null)
        {
            kinds.Add(ReportKind.Vehicle);
        }
        return kinds;
    }

    public async Task<SearchOptions> OptionsAsync(CancellationToken cancellationToken)
    {
        var partners = await db.Partners.AsNoTracking().OrderBy(p => p.Name)
            .Select(p => new SearchOption(p.Id, p.Name, p.IsActive)).ToListAsync(cancellationToken);
        var drivers = await db.Drivers.AsNoTracking().OrderBy(d => d.LastName).ThenBy(d => d.FirstName)
            .Select(d => new SearchOption(d.Id, d.FirstName + " " + d.LastName, d.IsActive)).ToListAsync(cancellationToken);
        var vehicles = await db.Vehicles.AsNoTracking().OrderBy(v => v.PlateNumber)
            .Select(v => new SearchOption(v.Id, v.PlateNumber + " · " + v.Make + " " + v.Model, v.IsActive)).ToListAsync(cancellationToken);
        return new SearchOptions(partners, drivers, vehicles);
    }

    public async Task<SearchResult> SearchAsync(SearchQuery query, bool includePay, CancellationToken cancellationToken)
    {
        var (rides, truncated) = await FindAsync(query, includePay, cancellationToken);
        return new SearchResult(rides, truncated, Totals(rides, includePay), PricingDefaults.Currency, AvailableReports(query));
    }

    public async Task<(byte[] Pdf, string FileName)> ReportAsync(SearchQuery query, ReportKind kind, bool includePay, CancellationToken cancellationToken)
    {
        if (!AvailableReports(query).Contains(kind))
        {
            throw new RequestValidationException("kind", Texts.Current.ReportNeedsFilter);
        }
        var (rides, truncated) = await FindAsync(query, includePay, cancellationToken);
        var subject = kind switch
        {
            ReportKind.Driver => await db.Drivers.Where(d => d.Id == query.DriverId).Select(d => d.FirstName + " " + d.LastName).FirstOrDefaultAsync(cancellationToken),
            ReportKind.Partner => await db.Partners.Where(p => p.Id == query.PartnerId).Select(p => p.Name).FirstOrDefaultAsync(cancellationToken),
            ReportKind.Vehicle => await db.Vehicles.Where(v => v.Id == query.VehicleId).Select(v => v.PlateNumber + " · " + v.Make + " " + v.Model).FirstOrDefaultAsync(cancellationToken),
            _ => null,
        };
        var report = new ReportRequest(kind, query, subject, rides, truncated, Totals(rides, includePay), PricingDefaults.Currency,
            includePay, OfficeTime.ToLocal(clock.GetUtcNow()));
        var file = kind switch
        {
            ReportKind.DailySheet => $"operations-{query.From:yyyy-MM-dd}.pdf",
            _ => $"{kind.ToString().ToLowerInvariant()}-{query.From:yyyy-MM-dd}-{query.To:yyyy-MM-dd}.pdf",
        };
        return (renderer.Render(report), file);
    }

    private async Task<(List<SearchRide> Rides, bool Truncated)> FindAsync(SearchQuery query, bool includePay, CancellationToken cancellationToken)
    {
        if (query.To < query.From || query.To.DayNumber - query.From.DayNumber >= MaxDays)
        {
            throw new RequestValidationException("to", Texts.Current.RangeTooLong(MaxDays));
        }
        var start = OfficeTime.ToUtc(query.From.ToDateTime(TimeOnly.MinValue))!.Value;
        var end = OfficeTime.ToUtc(query.To.AddDays(1).ToDateTime(TimeOnly.MinValue))!.Value;
        var bookings = db.Bookings.AsNoTracking()
            .Where(b => b.PickupAt >= start && b.PickupAt < end && b.Status != BookingStatus.AwaitingPayment);

        if (query.Statuses.Count > 0)
        {
            bookings = bookings.Where(b => query.Statuses.Contains(b.Status));
        }
        else if (!query.IncludeCancelled)
        {
            bookings = bookings.Where(b => b.Status != BookingStatus.Cancelled);
        }
        if (query.Source is { } source)
        {
            bookings = bookings.Where(b => b.Source == source);
        }
        if (query.PartnerId is { } partnerId)
        {
            bookings = bookings.Where(b => b.PartnerId == partnerId);
        }
        if (query.DriverId is { } driverId)
        {
            bookings = bookings.Where(b => b.Assignment != null && b.Assignment.DriverId == driverId);
        }
        if (query.VehicleId is { } vehicleId)
        {
            bookings = bookings.Where(b => b.Assignment != null && b.Assignment.VehicleId == vehicleId);
        }
        if (query.Category is { } category)
        {
            bookings = bookings.Where(b => b.VehicleCategory == category);
        }
        if (query.Unassigned)
        {
            bookings = bookings.Where(b => b.AssignmentId == null);
        }
        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var term = query.Text.Trim().ToLower();
            bookings = bookings.Where(b =>
                b.Reference.ToLower().Contains(term)
                || (b.Client.FirstName + " " + b.Client.LastName).ToLower().Contains(term)
                || b.Client.Phone.Contains(term)
                || (b.Client.Email != null && b.Client.Email.ToLower().Contains(term))
                || b.Pickup.Address.ToLower().Contains(term)
                || b.Dropoff.Address.ToLower().Contains(term)
                || (b.FlightNumber != null && b.FlightNumber.ToLower().Contains(term))
                || (b.ExternalReference != null && b.ExternalReference.ToLower().Contains(term)));
        }

        // Latest payment of the ride (a return leg is paid with its outbound).
        var rows = await bookings
            .OrderBy(b => b.PickupAt)
            .Select(b => new
            {
                Booking = b,
                b.Client,
                PartnerName = b.Partner != null ? b.Partner.Name : null,
                Driver = b.Assignment != null ? b.Assignment.Driver.FirstName + " " + b.Assignment.Driver.LastName : null,
                DriverId = b.Assignment != null ? (Guid?)b.Assignment.DriverId : null,
                VehicleId = b.Assignment != null ? (Guid?)b.Assignment.VehicleId : null,
                Plate = b.Assignment != null ? b.Assignment.Vehicle.PlateNumber : null,
                ShiftStatus = b.Assignment != null ? (AssignmentStatus?)b.Assignment.Status : null,
                Payment = db.Payments
                    .Where(p => p.BookingId == (b.ReturnOfBookingId ?? b.Id))
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => (PaymentStatus?)p.Status)
                    .FirstOrDefault(),
            })
            .Where(x => query.Payment == PaymentFilter.Any
                || (query.Payment == PaymentFilter.Paid && x.Payment == PaymentStatus.Succeeded)
                || (query.Payment == PaymentFilter.Refunded && (x.Payment == PaymentStatus.Refunded || x.Payment == PaymentStatus.PartiallyRefunded))
                || (query.Payment == PaymentFilter.NotPaidOnline && x.Payment == null))
            .Take(MaxRows + 1)
            .ToListAsync(cancellationToken);

        var rides = rows.Take(MaxRows).Select(x =>
        {
            var b = x.Booking;
            return new SearchRide(
                b.Id, b.Reference, b.Status, b.Source,
                RideTime.ToLocal(b.PickupAt, b.Pickup),
                b.DurationMinutes,
                b.PickupMeetingPoint?.Name ?? b.Pickup.Address,
                b.DropoffMeetingPoint?.Name ?? b.Dropoff.Address,
                $"{x.Client.FirstName} {x.Client.LastName}".Trim(),
                x.Client.Phone,
                x.Client.Email,
                x.PartnerName,
                b.ExternalReference,
                b.Passengers,
                b.LargeLuggage,
                b.SmallLuggage,
                b.VehicleCategory,
                b.DistanceKm,
                b.TotalPrice,
                includePay ? b.DriverPay : null,
                x.Payment,
                b.FlightNumber,
                x.DriverId,
                x.Driver,
                x.VehicleId,
                x.Plate,
                x.ShiftStatus);
        }).ToList();
        return (rides, rows.Count > MaxRows);
    }

    private static SearchTotals Totals(IReadOnlyList<SearchRide> rides, bool includePay)
    {
        var live = rides.Where(r => r.Status != BookingStatus.Cancelled).ToList();
        return new SearchTotals(
            live.Count,
            rides.Count - live.Count,
            live.Sum(r => r.TotalPrice),
            includePay ? live.Sum(r => r.DriverPay ?? 0) : null,
            live.Sum(r => r.DistanceKm),
            live.Sum(r => r.Passengers));
    }
}

/// <summary>Everything a PDF report needs.</summary>
public sealed record ReportRequest(
    ReportKind Kind,
    SearchQuery Query,
    /// <summary>The driver, partner or vehicle the report is about.</summary>
    string? Subject,
    IReadOnlyList<SearchRide> Rides,
    bool Truncated,
    SearchTotals Totals,
    string Currency,
    bool IncludePay,
    DateTime GeneratedAt);
