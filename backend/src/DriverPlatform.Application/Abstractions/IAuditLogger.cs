namespace DriverPlatform.Application.Abstractions;

public interface IAuditLogger
{
    /// <summary>Adds an audit entry to the current unit of work; it is saved with the next SaveChanges.</summary>
    void Log(string action, string entityType, string? entityId, object? changes = null);

    /// <summary>For actions not done by a signed-in staff member: a login, a driver on their link, a partner's API call.</summary>
    void LogAs(string actor, string action, string entityType, string? entityId, object? changes = null);
}
