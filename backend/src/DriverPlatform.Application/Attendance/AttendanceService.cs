using System.ComponentModel.DataAnnotations;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Localization;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Attendance;

public sealed record AttendanceTypeDto(Guid Id, string Name, string Code, string Color, bool MakesUnavailable, int SortOrder);

public sealed record AttendanceTypeRequest
{
    [Required, MaxLength(60)]
    public required string Name { get; init; }
    [Required, RegularExpression("^[A-Za-z0-9/]{1,3}$")]
    public required string Code { get; init; }
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$")]
    public required string Color { get; init; }
    public bool MakesUnavailable { get; init; }
    [Range(0, 999)]
    public int SortOrder { get; init; }
}

/// <summary>A row of the calendar: a driver or other staff member not excluded from it.</summary>
public sealed record AttendancePerson(
    Guid Id,
    string FirstName,
    string LastName,
    string? EmployeeNumber,
    DriverContractType ContractType,
    string? JobFunction,
    bool IsActive,
    DateOnly? HiredOn,
    DateOnly? LeftOn);

public sealed record AttendanceEntryDto(Guid Id, Guid DriverId, Guid TypeId, DateOnly Date, TimeOnly? StartTime, TimeOnly? EndTime, string? Notes);

public sealed record AttendanceResponse(
    IReadOnlyList<AttendanceTypeDto> Types,
    IReadOnlyList<AttendancePerson> People,
    IReadOnlyList<AttendanceEntryDto> Entries);

/// <summary>Puts one event on every day from From to To (replacing what was there). Without times it is a whole day.</summary>
public sealed record AttendanceRequest
{
    public Guid DriverId { get; init; }
    public Guid TypeId { get; init; }
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public TimeOnly? StartTime { get; init; }
    public TimeOnly? EndTime { get; init; }
    /// <summary>Leaves Saturdays and Sundays out of the range (e.g. two weeks of holiday).</summary>
    public bool SkipWeekends { get; init; }
    [MaxLength(500)]
    public string? Notes { get; init; }
}

public sealed record AttendanceClearRequest
{
    public Guid DriverId { get; init; }
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
}

public interface IAttendanceService
{
    Task<AttendanceResponse> ListAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<IReadOnlyList<AttendanceEntryDto>> SetAsync(AttendanceRequest request, CancellationToken cancellationToken);
    Task<int> ClearAsync(AttendanceClearRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<AttendanceTypeDto> CreateTypeAsync(AttendanceTypeRequest request, CancellationToken cancellationToken);
    Task<AttendanceTypeDto> UpdateTypeAsync(Guid id, AttendanceTypeRequest request, CancellationToken cancellationToken);
    Task DeleteTypeAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>
/// Attendance calendar of the staff: holidays, sick leave, days off, training… A whole day of a kind that
/// "makes unavailable" takes the person off the daily available count and blocks shifts that day.
/// </summary>
public sealed class AttendanceService(IAppDbContext db, IAuditLogger audit) : IAttendanceService
{
    /// <summary>Longest range one request may fill or clear.</summary>
    public const int MaxRangeDays = 93;
    /// <summary>Longest period the calendar or the statistics may show.</summary>
    public const int MaxListDays = 400;

    public async Task<AttendanceResponse> ListAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        CheckRange(from, to, MaxListDays);
        var types = await db.AttendanceTypes.AsNoTracking().OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);
        var people = await db.Drivers.AsNoTracking()
            .Where(d => !d.ExcludeFromAttendance)
            .Where(d => (d.HiredOn == null || d.HiredOn <= to) && (d.LeftOn == null || d.LeftOn >= from))
            .OrderBy(d => d.LastName).ThenBy(d => d.FirstName)
            .Select(d => new AttendancePerson(d.Id, d.FirstName, d.LastName, d.EmployeeNumber, d.ContractType, d.JobFunction,
                d.IsActive, d.HiredOn, d.LeftOn))
            .ToListAsync(cancellationToken);
        var entries = await db.AttendanceEntries.AsNoTracking()
            .Where(e => e.Date >= from && e.Date <= to && !e.Driver.ExcludeFromAttendance)
            .OrderBy(e => e.Date)
            .Select(e => new AttendanceEntryDto(e.Id, e.DriverId, e.TypeId, e.Date, e.StartTime, e.EndTime, e.Notes))
            .ToListAsync(cancellationToken);
        return new AttendanceResponse(types.Select(ToDto).ToList(), people, entries);
    }

    public async Task<IReadOnlyList<AttendanceEntryDto>> SetAsync(AttendanceRequest request, CancellationToken cancellationToken)
    {
        CheckRange(request.From, request.To, MaxRangeDays);
        if ((request.StartTime is null) != (request.EndTime is null))
        {
            throw new RequestValidationException("endTime", Texts.Current.GiveBothTimes);
        }
        if (request.StartTime is { } start && request.EndTime is { } end && end <= start)
        {
            throw new RequestValidationException("endTime", Texts.Current.EndBeforeStart);
        }
        if (!await db.Drivers.AnyAsync(d => d.Id == request.DriverId, cancellationToken))
        {
            throw new NotFoundException(Texts.Current.DriverNotFound);
        }
        if (!await db.AttendanceTypes.AnyAsync(t => t.Id == request.TypeId, cancellationToken))
        {
            throw new RequestValidationException("typeId", Texts.Current.RecordNotFound);
        }

        var days = Days(request.From, request.To)
            .Where(d => !request.SkipWeekends || (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday))
            .ToList();
        var existing = await db.AttendanceEntries
            .Where(e => e.DriverId == request.DriverId && e.Date >= request.From && e.Date <= request.To)
            .ToDictionaryAsync(e => e.Date, cancellationToken);
        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        var saved = new List<AttendanceEntry>();
        foreach (var day in days)
        {
            if (!existing.TryGetValue(day, out var entry))
            {
                entry = new AttendanceEntry { DriverId = request.DriverId, Date = day };
                db.AttendanceEntries.Add(entry);
            }
            entry.TypeId = request.TypeId;
            entry.StartTime = request.StartTime;
            entry.EndTime = request.EndTime;
            entry.Notes = notes;
            saved.Add(entry);
        }

        audit.Log("attendance.set", nameof(Driver), request.DriverId.ToString(),
            new { request.TypeId, request.From, request.To, request.StartTime, request.EndTime, Days = saved.Count });
        await db.SaveChangesAsync(cancellationToken);
        return saved.Select(ToDto).ToList();
    }

    public async Task<int> ClearAsync(AttendanceClearRequest request, CancellationToken cancellationToken)
    {
        CheckRange(request.From, request.To, MaxRangeDays);
        var entries = await db.AttendanceEntries
            .Where(e => e.DriverId == request.DriverId && e.Date >= request.From && e.Date <= request.To)
            .ToListAsync(cancellationToken);
        db.AttendanceEntries.RemoveRange(entries);
        audit.Log("attendance.cleared", nameof(Driver), request.DriverId.ToString(), new { request.From, request.To, entries.Count });
        await db.SaveChangesAsync(cancellationToken);
        return entries.Count;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = await db.AttendanceEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new NotFoundException(Texts.Current.RecordNotFound);
        db.AttendanceEntries.Remove(entry);
        audit.Log("attendance.deleted", nameof(Driver), entry.DriverId.ToString(), new { entry.Date });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AttendanceTypeDto> CreateTypeAsync(AttendanceTypeRequest request, CancellationToken cancellationToken)
    {
        var type = new AttendanceType();
        Apply(type, request);
        db.AttendanceTypes.Add(type);
        audit.Log("attendance_type.created", nameof(AttendanceType), type.Id.ToString(), request);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(type);
    }

    public async Task<AttendanceTypeDto> UpdateTypeAsync(Guid id, AttendanceTypeRequest request, CancellationToken cancellationToken)
    {
        var type = await FindTypeAsync(id, cancellationToken);
        Apply(type, request);
        audit.Log("attendance_type.updated", nameof(AttendanceType), id.ToString(), request);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(type);
    }

    /// <summary>Only a kind no day uses: the past calendar must keep its meaning.</summary>
    public async Task DeleteTypeAsync(Guid id, CancellationToken cancellationToken)
    {
        var type = await FindTypeAsync(id, cancellationToken);
        if (await db.AttendanceEntries.AnyAsync(e => e.TypeId == id, cancellationToken))
        {
            throw new ConflictException(Texts.Current.AttendanceTypeInUse);
        }
        db.AttendanceTypes.Remove(type);
        audit.Log("attendance_type.deleted", nameof(AttendanceType), id.ToString(), new { type.Name });
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// People who cannot work on some day between <paramref name="from"/> and <paramref name="to"/>: a whole-day
    /// event that makes them unavailable, or outside their employment dates. Value: why, for the dispatcher.
    /// </summary>
    public static async Task<Dictionary<Guid, string>> AbsentAsync(IAppDbContext db, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var absent = await db.AttendanceEntries.AsNoTracking()
            .Where(e => e.Date >= from && e.Date <= to && e.StartTime == null && e.Type.MakesUnavailable)
            .OrderBy(e => e.Date)
            .Select(e => new { e.DriverId, e.Type.Name, e.Date })
            .ToListAsync(cancellationToken);
        var result = new Dictionary<Guid, string>();
        foreach (var a in absent)
        {
            result.TryAdd(a.DriverId, Texts.Current.AbsentOn(a.Name, a.Date));
        }
        var notEmployed = await db.Drivers.AsNoTracking()
            .Where(d => d.HiredOn > from || d.LeftOn < to)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);
        foreach (var id in notEmployed)
        {
            result.TryAdd(id, Texts.Current.NotEmployed);
        }
        return result;
    }

    private static void CheckRange(DateOnly from, DateOnly to, int maxDays)
    {
        if (to < from || to.DayNumber - from.DayNumber >= maxDays)
        {
            throw new RequestValidationException("to", Texts.Current.RangeTooLong(maxDays));
        }
    }

    private static IEnumerable<DateOnly> Days(DateOnly from, DateOnly to)
    {
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            yield return d;
        }
    }

    private async Task<AttendanceType> FindTypeAsync(Guid id, CancellationToken cancellationToken) =>
        await db.AttendanceTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
        ?? throw new NotFoundException(Texts.Current.RecordNotFound);

    private static void Apply(AttendanceType type, AttendanceTypeRequest request)
    {
        type.Name = request.Name.Trim();
        type.Code = request.Code.Trim().ToUpperInvariant();
        type.Color = request.Color.ToLowerInvariant();
        type.MakesUnavailable = request.MakesUnavailable;
        type.SortOrder = request.SortOrder;
    }

    private static AttendanceTypeDto ToDto(AttendanceType t) => new(t.Id, t.Name, t.Code, t.Color, t.MakesUnavailable, t.SortOrder);

    private static AttendanceEntryDto ToDto(AttendanceEntry e) =>
        new(e.Id, e.DriverId, e.TypeId, e.Date, e.StartTime, e.EndTime, e.Notes);
}
