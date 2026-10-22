using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Localization;
using DriverPlatform.Application.Pricing;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Quotes;

public sealed class QuoteService(IAppDbContext db, IRoutingService routing, TimeProvider clock) : IQuoteService
{
    /// <summary>Pickups closer than this to the drop-off are refused.</summary>
    private const double MinTripMeters = 300;

    public async Task<BookingOptionsResponse> GetOptionsAsync(CancellationToken cancellationToken)
    {
        var settings = await db.LoadSettingsAsync(cancellationToken);
        var rates = (await db.VehicleRates.AsNoTracking().ToListAsync(cancellationToken)).OrderBy(r => r.Category);
        var extras = await db.ExtraFees.AsNoTracking().ToDictionaryAsync(f => f.Type, f => f.Amount, cancellationToken);
        var rules = await db.SurchargeRules.AsNoTracking().Where(r => r.IsActive).ToListAsync(cancellationToken);

        // Holidays are one rule per date; the public page shows them as a single line.
        const string holiday = "Public holiday";
        var texts = Texts.Current;
        var surcharges = rules.Where(r => !r.Name.StartsWith(holiday, StringComparison.Ordinal))
            .Select(r => new PublicSurcharge(r.NameTranslations.Pick(r.Name, texts.Language), r.Percent))
            .Concat(rules.Where(r => r.Name.StartsWith(holiday, StringComparison.Ordinal))
                .GroupBy(r => r.Percent)
                .Select(g => new PublicSurcharge(texts.PublicHolidays, g.Key)))
            .OrderBy(s => s.Percent).ThenBy(s => s.Name)
            .ToList();

        return new BookingOptionsResponse(
            rates.Select(r => new VehicleCapacity(r.Category, r.MaxPassengers, r.MaxLuggage, r.BaseFee, r.PricePerKm)).ToList(),
            extras,
            settings.ReturnDiscountPercent,
            settings.MinLeadTimeHours,
            PricingDefaults.Currency,
            surcharges);
    }

    public async Task<QuoteResponse> QuoteAsync(QuoteRequest request, CancellationToken cancellationToken)
    {
        var settings = await db.LoadSettingsAsync(cancellationToken);
        var zones = await db.Zones.AsNoTracking().Where(z => z.IsActive).ToListAsync(cancellationToken);
        // Enums are stored as text, so sort by enum value in memory (Sedan, Estate, Van).
        var rates = (await db.VehicleRates.AsNoTracking().ToListAsync(cancellationToken)).OrderBy(r => r.Category).ToList();
        var surcharges = await db.SurchargeRules.AsNoTracking().Where(r => r.IsActive).ToListAsync(cancellationToken);
        var extraFees = await db.ExtraFees.AsNoTracking().ToDictionaryAsync(f => f.Type, f => f.Amount, cancellationToken);

        var texts = Texts.Current;
        var errors = new ValidationErrors();
        ValidateTrip(request, errors, texts);
        var outboundPlan = PlanLeg(
            request.Pickup, request.Dropoff, request.PickupAt,
            request.PickupMeetingPoint, request.DropoffMeetingPoint,
            zones, settings, errors, texts, "");
        // The return leg runs the other way: the outbound drop-off meeting point becomes the pickup one.
        var returnPlan = request.Return is null ? null : PlanLeg(
            request.Dropoff, request.Pickup, request.Return.PickupAt,
            request.DropoffMeetingPoint, request.PickupMeetingPoint,
            zones, settings, errors, texts, "return.");
        errors.ThrowIfAny();

        var outbound = await RouteLegAsync(outboundPlan!, surcharges, texts, cancellationToken);
        RoutedLeg? inbound = null;
        if (returnPlan is not null)
        {
            if (returnPlan.PickupUtc < outbound.Leg.PickupAtUtc.AddMinutes(outbound.Leg.DurationMinutes))
            {
                throw new RequestValidationException("return.pickupAt", texts.ReturnAfterArrival);
            }
            inbound = await RouteLegAsync(returnPlan, surcharges, texts, cancellationToken);
        }

        var luggageUnits = TripCapacity.LuggageUnits(request.LargeLuggage, request.SmallLuggage);
        var childSeats = request.ChildSeats.Sum(s => s.Count);
        var extraLuggage = Math.Max(0, request.LargeLuggage - request.Passengers);

        var vehicles = rates.Select(rate =>
        {
            var reason = request.Passengers > rate.MaxPassengers ? texts.UpToPassengers(rate.MaxPassengers)
                : luggageUnits > rate.MaxLuggage ? texts.UpToLargeBags(rate.MaxLuggage)
                : null;

            var outboundPrice = PriceCalculator.Calculate(new PriceInput(
                rate, outbound.Leg.DistanceKm, outbound.Surcharges, outbound.FeeZones,
                childSeats, extraLuggage, request.NameSign, extraFees, Texts: texts));
            var returnPrice = inbound is null ? null : PriceCalculator.Calculate(new PriceInput(
                rate, inbound.Leg.DistanceKm, inbound.Surcharges, inbound.FeeZones,
                childSeats, extraLuggage, request.Return!.NameSign, extraFees, settings.ReturnDiscountPercent, texts));

            var total = outboundPrice.Total + (returnPrice?.Total ?? 0);
            return new VehicleOption(
                rate.Category, rate.MaxPassengers, rate.MaxLuggage, reason is null, reason,
                total, PriceCalculator.VatIncluded(total, settings.VatRatePercent),
                outboundPrice, returnPrice);
        }).ToList();

        return new QuoteResponse(outbound.Leg, inbound?.Leg, vehicles, PricingDefaults.Currency, settings.VatRatePercent);
    }

    private static void ValidateTrip(QuoteRequest request, ValidationErrors errors, Texts texts)
    {
        var seats = request.ChildSeats.Sum(s => s.Count);
        if (seats > Math.Min(request.Passengers, 3))
        {
            errors.Add("childSeats", texts.ChildSeatLimit);
        }
        if (request.ChildSeats.GroupBy(s => s.Group).Any(g => g.Count() > 1))
        {
            errors.Add("childSeats", texts.ChildSeatGroupOnce);
        }
    }

    private sealed record LegPlan(
        PointInput Pickup,
        PointInput Dropoff,
        DateTime PickupLocal,
        DateTimeOffset PickupUtc,
        Zone? PickupZone,
        Zone? DropoffZone,
        MeetingPoint? PickupMeetingPoint,
        MeetingPoint? DropoffMeetingPoint,
        IReadOnlyList<Zone> FeeZones);

    private sealed record RoutedLeg(QuoteLeg Leg, IReadOnlyList<SurchargeRule> Surcharges, IReadOnlyList<Zone> FeeZones);

    private LegPlan? PlanLeg(
        PointInput pickup,
        PointInput dropoff,
        DateTime pickupLocal,
        string? pickupMeetingPoint,
        string? dropoffMeetingPoint,
        List<Zone> zones,
        PricingSettings settings,
        ValidationErrors errors,
        Texts texts,
        string prefix)
    {
        var from = new Coordinate(pickup.Latitude, pickup.Longitude);
        var to = new Coordinate(dropoff.Latitude, dropoff.Longitude);
        var valid = true;

        if (!GeoMath.IsInServiceArea(from))
        {
            errors.Add(prefix + "pickup", texts.OutsideServiceArea);
            valid = false;
        }
        if (!GeoMath.IsInServiceArea(to))
        {
            errors.Add(prefix + "dropoff", texts.OutsideServiceArea);
            valid = false;
        }
        if (valid && GeoMath.DistanceMeters(from, to) < MinTripMeters)
        {
            errors.Add(prefix + "dropoff", texts.TooClose);
            valid = false;
        }

        var utc = ItalyTime.ToUtc(pickupLocal);
        var now = clock.GetUtcNow();
        if (utc is null)
        {
            errors.Add(prefix + "pickupAt", texts.TimeDoesNotExist);
            valid = false;
        }
        else if (utc < now.AddHours(settings.MinLeadTimeHours))
        {
            errors.Add(prefix + "pickupAt", texts.BookAhead(settings.MinLeadTimeHours));
            valid = false;
        }
        else if (utc > now.AddDays(PricingDefaults.MaxDaysAhead))
        {
            errors.Add(prefix + "pickupAt", texts.OneYearAhead);
            valid = false;
        }

        var (pickupZone, pickupPoint) = ResolveMeetingPoint(zones, from, pickupMeetingPoint, isPickup: true, prefix + "pickupMeetingPoint", errors, texts);
        var (dropoffZone, dropoffPoint) = ResolveMeetingPoint(zones, to, dropoffMeetingPoint, isPickup: false, prefix + "dropoffMeetingPoint", errors, texts);
        if (!valid || errors.Any)
        {
            return null;
        }

        var leg = new LegPlan(pickup, dropoff, pickupLocal, utc!.Value, pickupZone, dropoffZone, pickupPoint, dropoffPoint, []);
        return leg with { FeeZones = FeeZones(zones, Effective(leg.Pickup, pickupPoint), Effective(leg.Dropoff, dropoffPoint)) };
    }

    /// <summary>
    /// A no-car zone (Venice) moves the pickup or drop-off to one of its meeting points.
    /// Airports, stations and ports also have meeting points, for pickups only.
    /// </summary>
    private static (Zone? Zone, MeetingPoint? Point) ResolveMeetingPoint(
        List<Zone> zones, Coordinate point, string? requested, bool isPickup, string field, ValidationErrors errors, Texts texts)
    {
        var containing = zones.Where(z => GeoMath.Contains(z, point)).ToList();
        var noCar = containing.FirstOrDefault(z => z.Type == ZoneType.NoCarAccess);
        if (noCar is { MeetingPoints.Count: 0 })
        {
            errors.Add(field, texts.NoMeetingPointYet(noCar.Name));
            return (noCar, null);
        }

        var zone = noCar ?? (isPickup
            ? containing.FirstOrDefault(z => z.Type is ZoneType.Airport or ZoneType.TrainStation or ZoneType.Port && z.MeetingPoints.Count > 0)
            : null);
        if (zone is null)
        {
            return (containing.FirstOrDefault(), null);
        }

        if (!string.IsNullOrWhiteSpace(requested))
        {
            var chosen = zone.MeetingPoints.FirstOrDefault(m => string.Equals(m.Name, requested.Trim(), StringComparison.OrdinalIgnoreCase));
            if (chosen is null)
            {
                errors.Add(field, texts.NotAMeetingPoint(requested, zone.Name));
            }
            return (zone, chosen);
        }

        var nearest = zone.MeetingPoints
            .MinBy(m => GeoMath.DistanceMeters(point, new Coordinate(m.Latitude, m.Longitude)));
        return (zone, nearest);
    }

    private async Task<RoutedLeg> RouteLegAsync(LegPlan plan, List<SurchargeRule> surcharges, Texts texts, CancellationToken cancellationToken)
    {
        var route = await routing.GetRouteAsync(
            Effective(plan.Pickup, plan.PickupMeetingPoint),
            Effective(plan.Dropoff, plan.DropoffMeetingPoint),
            cancellationToken);
        var matched = MatchSurcharges(surcharges, plan.PickupLocal);

        var leg = new QuoteLeg(
            plan.Pickup, plan.Dropoff, plan.PickupLocal, plan.PickupUtc,
            ToDto(plan.PickupZone), ToDto(plan.DropoffZone),
            plan.PickupMeetingPoint, plan.DropoffMeetingPoint,
            Math.Round(route.DistanceKm, 1, MidpointRounding.AwayFromZero), route.DurationMinutes, route.IsEstimate, route.Path,
            matched.Select(s => new SurchargeDto(s.NameTranslations.Pick(s.Name, texts.Language), s.Percent)).ToList(),
            matched.Sum(s => s.Percent));
        return new RoutedLeg(leg, matched, plan.FeeZones);
    }

    private static List<SurchargeRule> MatchSurcharges(List<SurchargeRule> rules, DateTime localPickup) =>
        rules.Where(r => SurchargeMatcher.Matches(r, localPickup)).OrderBy(r => r.Name).ToList();

    /// <summary>Zones the car actually enters, so a ZTL fee applies once even if both ends are inside.</summary>
    private static List<Zone> FeeZones(List<Zone> zones, Coordinate from, Coordinate to) =>
        zones.Where(z => z.AccessFee > 0 && (GeoMath.Contains(z, from) || GeoMath.Contains(z, to))).OrderBy(z => z.Name).ToList();

    private static Coordinate Effective(PointInput point, MeetingPoint? meetingPoint) => meetingPoint is null
        ? new Coordinate(point.Latitude, point.Longitude)
        : new Coordinate(meetingPoint.Latitude, meetingPoint.Longitude);

    internal static ZoneDto? ToDto(Zone? zone) => zone is null ? null : new ZoneDto(
        zone.Id, zone.Name, zone.Type, zone.Shape, zone.CenterLatitude, zone.CenterLongitude, zone.RadiusMeters,
        zone.Polygon, zone.AccessFee, zone.MeetingPoints);
}
