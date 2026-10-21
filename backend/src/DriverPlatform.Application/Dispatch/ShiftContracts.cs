using System.ComponentModel.DataAnnotations;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Dispatch;

public sealed record ShiftRequest
{
    public Guid DriverId { get; init; }
    public Guid VehicleId { get; init; }
    [MinLength(1), MaxLength(20)]
    public List<Guid> BookingIds { get; init; } = [];
    [MaxLength(2000)]
    public string? Notes { get; init; }
}

public sealed record ShiftStatusRequest
{
    public AssignmentStatus Status { get; init; }
    [MaxLength(300)]
    public string? Reason { get; init; }
}

public sealed record ShiftDto(
    Guid Id,
    AssignmentStatus Status,
    DateOnly ShiftDate,
    DateTime StartsAt,
    DateTime EndsAt,
    Guid DriverId,
    string DriverName,
    string DriverPhone,
    Guid VehicleId,
    string VehicleLabel,
    string VehiclePlate,
    VehicleCategory VehicleCategory,
    string? Notes,
    IReadOnlyList<BookingListItem> Bookings,
    DateTimeOffset? SentAt,
    DateTimeOffset? SeenAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<MessageSummary> Messages);

public sealed record MessageSummary(
    NotificationKind Kind,
    NotificationChannel Channel,
    NotificationStatus Status,
    string Recipient,
    string? Error,
    DateTimeOffset CreatedAt);

/// <summary>The saved shift plus non-blocking warnings (tight connections, documents expiring soon).</summary>
public sealed record ShiftResult(ShiftDto Shift, IReadOnlyList<string> Warnings);

public sealed record DriverOption(Guid Id, string Name, bool Available, IReadOnlyList<string> Problems);

public sealed record VehicleOption(Guid Id, string Label, string PlateNumber, VehicleCategory Category, bool Available, IReadOnlyList<string> Problems);

/// <summary>Who can take these bookings: free at that time, documents valid, car big enough.</summary>
public sealed record AvailabilityResponse(IReadOnlyList<DriverOption> Drivers, IReadOnlyList<VehicleOption> Vehicles);

public sealed record DocumentAlert(string Owner, string Kind, Guid OwnerId, string Document, DateOnly ExpiresOn, bool Expired);

public sealed record DashboardResponse(
    int TodayRides,
    int TodayCompleted,
    int InProgress,
    int UnassignedNext48h,
    int AwaitingPayment,
    int ShiftsToConfirm,
    IReadOnlyList<BookingListItem> NextUnassigned,
    IReadOnlyList<DocumentAlert> DocumentAlerts);
