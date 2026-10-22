using System.ComponentModel.DataAnnotations;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Auth;
using DriverPlatform.Application.Common;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using DriverPlatform.Application.Localization;

namespace DriverPlatform.Application.Staff;

public sealed record StaffDto(
    Guid Id, string Email, string FullName, StaffRole Role, bool IsActive, bool MustChangePassword,
    DateTimeOffset? LastLoginAt, DateTimeOffset? LockedUntil, DateTimeOffset CreatedAt);

public sealed record CreateStaffRequest
{
    [Required, EmailAddress, MaxLength(254)]
    public required string Email { get; init; }
    [Required, MaxLength(120)]
    public required string FullName { get; init; }
    public StaffRole Role { get; init; } = StaffRole.Dispatcher;
}

public sealed record UpdateStaffRequest
{
    [Required, MaxLength(120)]
    public required string FullName { get; init; }
    public StaffRole Role { get; init; }
    public bool IsActive { get; init; } = true;
}

/// <summary>A temporary password, shown once; it must be changed at the first sign-in.</summary>
public sealed record TemporaryPasswordResponse(StaffDto User, string TemporaryPassword);

public interface IStaffAdminService
{
    Task<IReadOnlyList<StaffDto>> ListAsync(CancellationToken cancellationToken);
    Task<TemporaryPasswordResponse> CreateAsync(CreateStaffRequest request, CancellationToken cancellationToken);
    Task<StaffDto> UpdateAsync(Guid id, UpdateStaffRequest request, CancellationToken cancellationToken);
    Task<TemporaryPasswordResponse> ResetPasswordAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Admins manage panel accounts. The last active admin can never be removed or demoted.</summary>
public sealed class StaffAdminService(
    IAppDbContext db, IPasswordHasher hasher, IAuditLogger audit, ICurrentUser currentUser, IStaffSessions sessions) : IStaffAdminService
{
    public async Task<IReadOnlyList<StaffDto>> ListAsync(CancellationToken cancellationToken)
    {
        var users = await db.StaffUsers.AsNoTracking().OrderBy(u => u.FullName).ToListAsync(cancellationToken);
        return users.Select(ToDto).ToList();
    }

    public async Task<TemporaryPasswordResponse> CreateAsync(CreateStaffRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.StaffUsers.AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new RequestValidationException("email", Texts.Current.EmailTaken);
        }
        var password = PasswordPolicy.Temporary();
        var user = new StaffUser
        {
            Email = email,
            FullName = request.FullName.Trim(),
            Role = request.Role,
            PasswordHash = hasher.Hash(password),
            MustChangePassword = true,
        };
        db.StaffUsers.Add(user);
        audit.Log("staff.created", nameof(StaffUser), user.Id.ToString(), new { email, request.Role });
        await db.SaveChangesAsync(cancellationToken);
        return new TemporaryPasswordResponse(ToDto(user), password);
    }

    public async Task<StaffDto> UpdateAsync(Guid id, UpdateStaffRequest request, CancellationToken cancellationToken)
    {
        var user = await FindAsync(id, cancellationToken);
        var losesAdmin = user is { Role: StaffRole.Admin, IsActive: true } && (request.Role != StaffRole.Admin || !request.IsActive);
        if (losesAdmin && !await db.StaffUsers.AnyAsync(u => u.Id != id && u.Role == StaffRole.Admin && u.IsActive, cancellationToken))
        {
            throw new ConflictException(Texts.Current.LastAdmin);
        }
        if (id == currentUser.StaffUserId && !request.IsActive)
        {
            throw new ConflictException(Texts.Current.CannotDeactivateSelf);
        }

        var signOut = user.Role != request.Role || user.IsActive != request.IsActive;
        user.FullName = request.FullName.Trim();
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        if (signOut)
        {
            // Open sessions must not keep the old role, nor survive a deactivation.
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        }
        audit.Log("staff.updated", nameof(StaffUser), id.ToString(), request);
        await db.SaveChangesAsync(cancellationToken);
        sessions.Invalidate(id);
        return ToDto(user);
    }

    public async Task<TemporaryPasswordResponse> ResetPasswordAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await FindAsync(id, cancellationToken);
        var password = PasswordPolicy.Temporary();
        user.PasswordHash = hasher.Hash(password);
        user.MustChangePassword = true;
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        audit.Log("staff.password_reset", nameof(StaffUser), id.ToString());
        await db.SaveChangesAsync(cancellationToken);
        sessions.Invalidate(id);
        return new TemporaryPasswordResponse(ToDto(user), password);
    }

    private async Task<StaffUser> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await db.StaffUsers.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
        ?? throw new NotFoundException(Texts.Current.UserNotFound);

    private static StaffDto ToDto(StaffUser u) => new(
        u.Id, u.Email, u.FullName, u.Role, u.IsActive, u.MustChangePassword, u.LastLoginAt, u.LockedUntil, u.CreatedAt);
}
