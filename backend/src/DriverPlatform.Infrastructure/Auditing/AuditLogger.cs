using System.Text.Json;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Infrastructure.Persistence;

namespace DriverPlatform.Infrastructure.Auditing;

public sealed class AuditLogger(AppDbContext db, ICurrentUser currentUser) : IAuditLogger
{
    public void Log(string action, string entityType, string? entityId, object? changes = null) =>
        Add(currentUser.StaffUserId, currentUser.Email ?? "system", action, entityType, entityId, changes);

    public void LogAs(string actor, string action, string entityType, string? entityId, object? changes = null) =>
        Add(null, actor, action, entityType, entityId, changes);

    private void Add(Guid? staffUserId, string actor, string action, string entityType, string? entityId, object? changes)
    {
        db.AuditLogs.Add(new AuditLog
        {
            StaffUserId = staffUserId,
            Actor = actor,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Changes = changes is null ? null : JsonSerializer.Serialize(changes),
            IpAddress = currentUser.IpAddress,
        });
    }
}
