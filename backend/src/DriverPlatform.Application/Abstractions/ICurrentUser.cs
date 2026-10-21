namespace DriverPlatform.Application.Abstractions;

/// <summary>The caller of the current request.</summary>
public interface ICurrentUser
{
    Guid? StaffUserId { get; }
    string? Email { get; }
    string? IpAddress { get; }
}
