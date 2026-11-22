using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Localization;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Dispatch;

/// <summary>Where a deadline comes from, so the page can name it and renew the right record.</summary>
public enum DeadlineSource
{
    /// <summary>A driver's document (licence, NCC, certificate…).</summary>
    DriverDocument,
    Insurance,
    Inspection,
    Service,
    /// <summary>One of a vehicle's own deadlines (road tax, tachograph…), by date or odometer.</summary>
    VehicleDeadline,
}

public enum DeadlineState
{
    Expired,
    DueSoon,
    Later,
}

public enum DeadlineOwner
{
    Driver,
    Vehicle,
}

public sealed record ExpirationItem(
    DeadlineSource Source,
    DeadlineOwner OwnerKind,
    Guid OwnerId,
    string OwnerName,
    /// <summary>The car's make and model, or the driver's phone.</summary>
    string? OwnerDetail,
    bool OwnerActive,
    /// <summary>The document or vehicle deadline renewed; null for insurance, inspection and service (fields of the car).</summary>
    Guid? RecordId,
    DriverDocumentType? DocumentType,
    /// <summary>Title of a vehicle deadline.</summary>
    string? Title,
    /// <summary>Document number, or the notes of a vehicle deadline.</summary>
    string? Reference,
    DateOnly? DueOn,
    int? DueKm,
    /// <summary>Negative when overdue.</summary>
    int? DaysLeft,
    int? KmLeft,
    DeadlineState State);

/// <summary>A driver or car missing a date the dispatch rules need (no licence, no insurance date…).</summary>
public sealed record MissingDocument(DeadlineOwner OwnerKind, Guid OwnerId, string OwnerName, bool OwnerActive, string Problem);

public sealed record ExpirationsResponse(
    IReadOnlyList<ExpirationItem> Items,
    IReadOnlyList<MissingDocument> Missing,
    DateOnly Today,
    int AlertDays,
    int AlertKm);

public sealed record RenewDeadlineRequest
{
    public DeadlineSource Source { get; init; }
    public Guid OwnerId { get; init; }
    public Guid? RecordId { get; init; }
    public DateOnly? DueOn { get; init; }
    public int? DueKm { get; init; }
}

public interface IExpirationService
{
    Task<ExpirationsResponse> ListAsync(bool includeInactive, CancellationToken cancellationToken);
    Task RenewAsync(RenewDeadlineRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Admin → Expirations: every dated obligation of the drivers and cars in one list, most urgent first,
/// with the same thresholds as the dashboard (30 days, 1,000 km).
/// </summary>
public sealed class ExpirationService(IAppDbContext db, IAuditLogger audit, TimeProvider clock) : IExpirationService
{
    public async Task<ExpirationsResponse> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(OfficeTime.ToLocal(clock.GetUtcNow()));
        var drivers = await db.Drivers.AsNoTracking().Include(d => d.Documents)
            .Where(d => includeInactive || d.IsActive).ToListAsync(cancellationToken);
        var vehicles = await db.Vehicles.AsNoTracking().Include(v => v.Expirations)
            .Where(v => includeInactive || v.IsActive).ToListAsync(cancellationToken);

        var items = new List<ExpirationItem>();
        foreach (var d in drivers)
        {
            var name = $"{d.FirstName} {d.LastName}";
            items.AddRange(d.Documents.Where(x => x.ExpiresOn is not null).Select(x => Item(
                DeadlineSource.DriverDocument, DeadlineOwner.Driver, d.Id, name, d.Phone, d.IsActive, x.Id,
                x.Type, null, x.Number, x.ExpiresOn, null, null, today)));
        }
        foreach (var v in vehicles)
        {
            var name = string.IsNullOrEmpty(v.Code) ? v.PlateNumber : $"{v.Code} · {v.PlateNumber}";
            var detail = $"{v.Make} {v.Model}";
            ExpirationItem Car(DeadlineSource source, DateOnly date) =>
                Item(source, DeadlineOwner.Vehicle, v.Id, name, detail, v.IsActive, null, null, null, null, date, null, null, today);
            if (v.InsuranceExpiresOn is { } insurance)
            {
                items.Add(Car(DeadlineSource.Insurance, insurance));
            }
            if (v.InspectionExpiresOn is { } inspection)
            {
                items.Add(Car(DeadlineSource.Inspection, inspection));
            }
            if (v.NextServiceOn is { } service)
            {
                items.Add(Car(DeadlineSource.Service, service));
            }
            items.AddRange(v.Expirations.Select(e => Item(
                DeadlineSource.VehicleDeadline, DeadlineOwner.Vehicle, v.Id, name, detail, v.IsActive, e.Id,
                null, e.Title, e.Notes, e.DueOn, e.DueKm, v.MileageKm, today)));
        }

        var missing = drivers
            .SelectMany(d => DocumentRules.Missing(d).Select(p =>
                new MissingDocument(DeadlineOwner.Driver, d.Id, $"{d.FirstName} {d.LastName}", d.IsActive, p)))
            .Concat(vehicles.SelectMany(v => DocumentRules.Missing(v).Select(p =>
                new MissingDocument(DeadlineOwner.Vehicle, v.Id, v.PlateNumber, v.IsActive, p))))
            .OrderBy(m => m.OwnerKind).ThenBy(m => m.OwnerName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        // Most urgent first; within a state by date, odometer-only deadlines after the dated ones by kilometres left.
        var sorted = items
            .OrderBy(i => i.State)
            .ThenBy(i => i.DueOn is null)
            .ThenBy(i => i.DueOn)
            .ThenBy(i => i.KmLeft)
            .ThenBy(i => i.OwnerName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return new ExpirationsResponse(sorted, missing, today, DocumentRules.AlertDays, VehicleLogService.AlertKm);
    }

    public async Task RenewAsync(RenewDeadlineRequest request, CancellationToken cancellationToken)
    {
        if (request.Source != DeadlineSource.VehicleDeadline && request.DueOn is null)
        {
            throw new RequestValidationException("dueOn", Texts.Current.GiveNewDate);
        }
        switch (request.Source)
        {
            case DeadlineSource.DriverDocument:
            {
                var document = await db.DriverDocuments
                    .FirstOrDefaultAsync(d => d.Id == request.RecordId && d.DriverId == request.OwnerId, cancellationToken)
                    ?? throw new NotFoundException(Texts.Current.RecordNotFound);
                document.ExpiresOn = request.DueOn;
                audit.Log("driver.document_renewed", "Driver", request.OwnerId.ToString(), new { document.Type, request.DueOn });
                break;
            }
            case DeadlineSource.Insurance or DeadlineSource.Inspection or DeadlineSource.Service:
            {
                var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == request.OwnerId, cancellationToken)
                    ?? throw new NotFoundException(Texts.Current.VehicleNotFound);
                switch (request.Source)
                {
                    case DeadlineSource.Insurance:
                        vehicle.InsuranceExpiresOn = request.DueOn;
                        break;
                    case DeadlineSource.Inspection:
                        vehicle.InspectionExpiresOn = request.DueOn;
                        break;
                    default:
                        vehicle.NextServiceOn = request.DueOn;
                        break;
                }
                audit.Log("vehicle.deadline_renewed", "Vehicle", request.OwnerId.ToString(), new { request.Source, request.DueOn });
                break;
            }
            case DeadlineSource.VehicleDeadline:
            {
                if (request.DueOn is null && request.DueKm is null)
                {
                    throw new RequestValidationException("dueOn", Texts.Current.GiveDateOrKm);
                }
                if (request.DueKm is < 0 or > 2_000_000)
                {
                    throw new RequestValidationException("dueKm", Texts.Current.GiveDateOrKm);
                }
                var expiration = await db.VehicleExpirations
                    .FirstOrDefaultAsync(e => e.Id == request.RecordId && e.VehicleId == request.OwnerId, cancellationToken)
                    ?? throw new NotFoundException(Texts.Current.RecordNotFound);
                expiration.DueOn = request.DueOn;
                expiration.DueKm = request.DueKm;
                audit.Log("vehicle.expiration_updated", "Vehicle", request.OwnerId.ToString(),
                    new { expiration.Title, request.DueOn, request.DueKm });
                break;
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static ExpirationItem Item(
        DeadlineSource source, DeadlineOwner kind, Guid ownerId, string ownerName, string? detail, bool active, Guid? recordId,
        DriverDocumentType? documentType, string? title, string? reference, DateOnly? dueOn, int? dueKm, int? mileageKm,
        DateOnly today)
    {
        int? daysLeft = dueOn is { } date ? date.DayNumber - today.DayNumber : null;
        int? kmLeft = dueKm is { } km && mileageKm is { } mileage ? km - mileage : null;
        var state = DeadlineState.Later;
        if (daysLeft < 0 || kmLeft <= 0)
        {
            state = DeadlineState.Expired;
        }
        else if (daysLeft <= DocumentRules.AlertDays || kmLeft <= VehicleLogService.AlertKm)
        {
            state = DeadlineState.DueSoon;
        }
        return new ExpirationItem(source, kind, ownerId, ownerName, detail, active, recordId, documentType, title,
            string.IsNullOrWhiteSpace(reference) ? null : reference, dueOn, dueKm, daysLeft, kmLeft, state);
    }
}
