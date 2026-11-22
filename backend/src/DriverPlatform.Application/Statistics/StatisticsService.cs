using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Localization;
using DriverPlatform.Application.Pricing;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Statistics;

public sealed record StatisticsQuery
{
    /// <summary>Office dates of the pickup, inclusive; at most 366 days.</summary>
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
}

/// <summary>The headline numbers of a period. Cancelled rides count only in <see cref="Cancelled"/>.</summary>
/// <param name="DriverPay">Admins only.</param>
public sealed record StatisticsTotals(
    int Rides, decimal Revenue, decimal AverageRide, decimal Km, int Passengers,
    int Cancelled, decimal CancelledRate, int Customers, int NewCustomers, decimal Refunded, decimal? DriverPay);

public enum StatisticsBucket { Day, Week, Month }

/// <param name="Start">First office day of the bucket.</param>
public sealed record StatisticsPoint(DateOnly Start, int Rides, decimal Revenue, int PreviousRides, decimal PreviousRevenue);

/// <param name="Key">Stable id for colours and links (enum name, partner or driver id).</param>
public sealed record StatisticsShare(string Key, string Label, int Rides, decimal Revenue, decimal Km, decimal? Pay);

public sealed record StatisticsResult(
    DateOnly From, DateOnly To, DateOnly PreviousFrom, DateOnly PreviousTo,
    StatisticsTotals Totals, StatisticsTotals Previous,
    StatisticsBucket Bucket, IReadOnlyList<StatisticsPoint> Series,
    IReadOnlyList<StatisticsShare> Channels, IReadOnlyList<StatisticsShare> Categories,
    IReadOnlyList<StatisticsShare> Drivers, IReadOnlyList<StatisticsShare> Vehicles,
    /// <summary>Rides by office weekday (0 = Monday) and hour: 7 × 24.</summary>
    IReadOnlyList<IReadOnlyList<int>> Heatmap,
    /// <summary>How long before the pickup the ride was booked: &lt;1 day, 1–3, 4–7, 8–30, more.</summary>
    IReadOnlyList<int> LeadTime,
    int Unassigned,
    string Currency);

public interface IStatisticsService
{
    Task<StatisticsResult> GetAsync(StatisticsQuery query, bool includePay, CancellationToken cancellationToken);
}

/// <summary>
/// Admin → Statistics: rides and revenue of a period against the period just before it, by channel,
/// class, driver and vehicle, with the busy hours and how far ahead clients book.
/// </summary>
public sealed class StatisticsService(IAppDbContext db) : IStatisticsService
{
    public const int MaxDays = 366;

    private sealed class Row
    {
        public Guid Id { get; init; }
        public Guid ClientId { get; init; }
        public BookingStatus Status { get; init; }
        public BookingSource Source { get; init; }
        public Guid? PartnerId { get; init; }
        public string? PartnerName { get; init; }
        public VehicleCategory Category { get; init; }
        public DateTimeOffset PickupAt { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public decimal Price { get; init; }
        public decimal Km { get; init; }
        public int Passengers { get; init; }
        public decimal? Pay { get; init; }
        public Guid? DriverId { get; init; }
        public string? DriverName { get; init; }
        public Guid? VehicleId { get; init; }
        public string? Plate { get; init; }
    }

    public async Task<StatisticsResult> GetAsync(StatisticsQuery query, bool includePay, CancellationToken cancellationToken)
    {
        var days = query.To.DayNumber - query.From.DayNumber + 1;
        if (days < 1 || days > MaxDays)
        {
            throw new RequestValidationException("to", Texts.Current.RangeTooLong(MaxDays));
        }
        var (prevFrom, prevTo) = PreviousPeriod(query.From, query.To);

        var rows = await LoadAsync(prevFrom, query.To, cancellationToken);
        var current = rows.Where(r => Day(r) >= query.From).ToList();
        var previous = rows.Where(r => Day(r) < query.From).ToList();

        var currentIds = current.Select(r => r.Id).ToList();
        var previousIds = previous.Select(r => r.Id).ToList();
        var refunds = await db.Refunds.AsNoTracking()
            .Where(f => currentIds.Contains(f.Payment.BookingId) || previousIds.Contains(f.Payment.BookingId))
            .Select(f => new { f.Payment.BookingId, f.Amount })
            .ToListAsync(cancellationToken);
        decimal Refunded(List<Row> set)
        {
            var ids = set.Select(r => r.Id).ToHashSet();
            return refunds.Where(f => ids.Contains(f.BookingId)).Sum(f => f.Amount);
        }

        // A client is new when their first counted ride ever falls in the period.
        var clientIds = current.Concat(previous).Select(r => r.ClientId).Distinct().ToList();
        var firstRides = await db.Bookings.AsNoTracking()
            .Where(b => clientIds.Contains(b.ClientId) && b.Status != BookingStatus.AwaitingPayment && b.Status != BookingStatus.Cancelled)
            .GroupBy(b => b.ClientId)
            .Select(g => new { ClientId = g.Key, First = g.Min(b => b.PickupAt) })
            .ToDictionaryAsync(x => x.ClientId, x => x.First, cancellationToken);

        StatisticsTotals Totals(List<Row> set, DateOnly from)
        {
            var done = set.Where(r => r.Status != BookingStatus.Cancelled).ToList();
            var revenue = done.Sum(r => r.Price);
            var cancelled = set.Count - done.Count;
            var start = OfficeTime.ToUtc(from.ToDateTime(TimeOnly.MinValue))!.Value;
            var customers = done.Select(r => r.ClientId).Distinct().ToList();
            return new StatisticsTotals(
                done.Count, revenue, done.Count == 0 ? 0 : Math.Round(revenue / done.Count, 2),
                done.Sum(r => r.Km), done.Sum(r => r.Passengers),
                cancelled, set.Count == 0 ? 0 : Math.Round((decimal)cancelled / set.Count, 4),
                customers.Count, customers.Count(c => firstRides.TryGetValue(c, out var first) && first >= start),
                Refunded(set), includePay ? done.Sum(r => r.Pay ?? 0) : null);
        }

        var doneNow = current.Where(r => r.Status != BookingStatus.Cancelled).ToList();
        var donePrev = previous.Where(r => r.Status != BookingStatus.Cancelled).ToList();
        var bucket = days <= 45 ? StatisticsBucket.Day : days <= 120 ? StatisticsBucket.Week : StatisticsBucket.Month;

        return new StatisticsResult(
            query.From, query.To, prevFrom, prevTo,
            Totals(current, query.From), Totals(previous, prevFrom),
            bucket, Series(query.From, query.To, prevFrom, bucket, doneNow, donePrev),
            Shares(doneNow, r => r.PartnerId is { } p ? (p.ToString(), r.PartnerName ?? "") : (r.Source.ToString(), r.Source.ToString()), includePay),
            Shares(doneNow, r => (r.Category.ToString(), r.Category.ToString()), includePay),
            Shares(doneNow.Where(r => r.DriverId is not null), r => (r.DriverId!.Value.ToString(), r.DriverName ?? ""), includePay),
            Shares(doneNow.Where(r => r.VehicleId is not null), r => (r.VehicleId!.Value.ToString(), r.Plate ?? ""), includePay),
            Heatmap(doneNow), LeadTime(doneNow),
            doneNow.Count(r => r.DriverId is null && r.Status == BookingStatus.New),
            PricingDefaults.Currency);
    }

    /// <summary>
    /// The period just before, as long as this one; whole months compare with the same number of whole
    /// months (November with October, not with 2–31 October).
    /// </summary>
    public static (DateOnly From, DateOnly To) PreviousPeriod(DateOnly from, DateOnly to)
    {
        var prevTo = from.AddDays(-1);
        if (from.Day == 1 && to.AddDays(1).Day == 1)
        {
            var months = (to.Year - from.Year) * 12 + to.Month - from.Month + 1;
            return (from.AddMonths(-months), prevTo);
        }
        return (prevTo.AddDays(from.DayNumber - to.DayNumber), prevTo);
    }

    private async Task<List<Row>> LoadAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var start = OfficeTime.ToUtc(from.ToDateTime(TimeOnly.MinValue))!.Value;
        var end = OfficeTime.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue))!.Value;
        return await db.Bookings.AsNoTracking()
            .Where(b => b.PickupAt >= start && b.PickupAt < end && b.Status != BookingStatus.AwaitingPayment)
            .Select(b => new Row
            {
                Id = b.Id,
                ClientId = b.ClientId,
                Status = b.Status,
                Source = b.Source,
                PartnerId = b.PartnerId,
                PartnerName = b.Partner != null ? b.Partner.Name : null,
                Category = b.VehicleCategory,
                PickupAt = b.PickupAt,
                CreatedAt = b.CreatedAt,
                Price = b.TotalPrice,
                Km = b.DistanceKm,
                Passengers = b.Passengers,
                Pay = b.DriverPay,
                DriverId = b.Assignment != null ? b.Assignment.DriverId : null,
                DriverName = b.Assignment != null ? b.Assignment.Driver.FirstName + " " + b.Assignment.Driver.LastName : null,
                VehicleId = b.Assignment != null ? b.Assignment.VehicleId : null,
                Plate = b.Assignment != null ? b.Assignment.Vehicle.PlateNumber : null,
            })
            .ToListAsync(cancellationToken);
    }

    private static DateOnly Day(Row r) => DateOnly.FromDateTime(OfficeTime.ToLocal(r.PickupAt));

    /// <summary>Start of the bucket holding <paramref name="day"/>: the day, its Monday, or the 1st.</summary>
    public static DateOnly BucketStart(DateOnly day, StatisticsBucket bucket) => bucket switch
    {
        StatisticsBucket.Week => day.AddDays(-(((int)day.DayOfWeek + 6) % 7)),
        StatisticsBucket.Month => new DateOnly(day.Year, day.Month, 1),
        _ => day,
    };

    private static List<StatisticsPoint> Series(
        DateOnly from, DateOnly to, DateOnly prevFrom, StatisticsBucket bucket, List<Row> now, List<Row> before)
    {
        // The previous period lines up by position: its n-th bucket sits under ours.
        List<DateOnly> Starts(DateOnly a, DateOnly b)
        {
            var list = new List<DateOnly>();
            for (var d = BucketStart(a, bucket); d <= b;)
            {
                list.Add(d);
                d = bucket switch { StatisticsBucket.Week => d.AddDays(7), StatisticsBucket.Month => d.AddMonths(1), _ => d.AddDays(1) };
            }
            return list;
        }
        var starts = Starts(from, to);
        var prevStarts = Starts(prevFrom, from.AddDays(-1));
        var nowBy = now.GroupBy(r => BucketStart(Day(r), bucket)).ToDictionary(g => g.Key, g => (g.Count(), g.Sum(r => r.Price)));
        var prevBy = before.GroupBy(r => BucketStart(Day(r), bucket)).ToDictionary(g => g.Key, g => (g.Count(), g.Sum(r => r.Price)));
        // The n-th bucket of one period sits beside the n-th of the other (1 November beside 1 October).
        return starts.Select((s, i) =>
        {
            var (rides, revenue) = nowBy.GetValueOrDefault(s);
            var j = i;
            var (pr, prev) = j < prevStarts.Count ? prevBy.GetValueOrDefault(prevStarts[j]) : default;
            return new StatisticsPoint(s, rides, revenue, pr, prev);
        }).ToList();
    }

    private static List<StatisticsShare> Shares(IEnumerable<Row> rows, Func<Row, (string Key, string Label)> by, bool includePay) =>
        rows.GroupBy(by)
            .Select(g => new StatisticsShare(g.Key.Key, g.Key.Label, g.Count(), g.Sum(r => r.Price), g.Sum(r => r.Km),
                includePay ? g.Sum(r => r.Pay ?? 0) : null))
            .OrderByDescending(s => s.Revenue).ThenByDescending(s => s.Rides)
            .ToList();

    private static List<IReadOnlyList<int>> Heatmap(List<Row> rows)
    {
        var grid = Enumerable.Range(0, 7).Select(_ => new int[24]).ToArray();
        foreach (var r in rows)
        {
            var local = OfficeTime.ToLocal(r.PickupAt);
            grid[((int)local.DayOfWeek + 6) % 7][local.Hour]++;
        }
        return grid.Select(g => (IReadOnlyList<int>)g).ToList();
    }

    private static List<int> LeadTime(List<Row> rows)
    {
        var buckets = new int[5];
        foreach (var r in rows)
        {
            var d = (r.PickupAt - r.CreatedAt).TotalDays;
            buckets[d < 1 ? 0 : d < 4 ? 1 : d < 8 ? 2 : d < 31 ? 3 : 4]++;
        }
        return [.. buckets];
    }
}
