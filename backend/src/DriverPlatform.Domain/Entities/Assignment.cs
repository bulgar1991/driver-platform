using DriverPlatform.Domain.Common;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Domain.Entities;

/// <summary>A shift: one or more bookings given to a driver and a vehicle.</summary>
public class Assignment : Entity
{
    public Guid DriverId { get; set; }
    public Driver Driver { get; set; } = null!;
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public AssignmentStatus Status { get; set; }
    /// <summary>Italian date of the first pickup; shifts are listed by day.</summary>
    public DateOnly ShiftDate { get; set; }
    /// <summary>First pickup and last expected drop-off, kept up to date so overlaps can be checked in the database.</summary>
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string? DispatcherNotes { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? SeenAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    public ICollection<Booking> Bookings { get; set; } = [];
}
