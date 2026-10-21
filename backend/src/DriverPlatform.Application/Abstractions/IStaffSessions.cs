namespace DriverPlatform.Application.Abstractions;

/// <summary>Checks that a staff token still belongs to an active user with the same security stamp.</summary>
public interface IStaffSessions
{
    Task<bool> IsValidAsync(Guid userId, string? stamp, CancellationToken cancellationToken);
    /// <summary>Call after changing a user's stamp or status, so the next request sees it at once.</summary>
    void Invalidate(Guid userId);
}
