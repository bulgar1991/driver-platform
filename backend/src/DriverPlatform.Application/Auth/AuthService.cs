using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Auth;

public sealed class AuthService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IAuditLogger audit,
    IStaffSessions sessions,
    TimeProvider clock) : IAuthService
{
    public const int MaxFailedLogins = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.StaffUsers.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        var now = clock.GetUtcNow();

        if (user is null || !user.IsActive)
        {
            return new LoginResult(LoginOutcome.InvalidCredentials);
        }

        if (user.LockedUntil > now)
        {
            return new LoginResult(LoginOutcome.LockedOut);
        }

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedLogins)
            {
                user.LockedUntil = now.Add(LockoutDuration);
                user.FailedLoginCount = 0;
                audit.LogAs(user.Email, "auth.locked_out", nameof(StaffUser), user.Id.ToString());
            }
            await db.SaveChangesAsync(cancellationToken);
            return new LoginResult(LoginOutcome.InvalidCredentials);
        }

        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;
        audit.LogAs(user.Email, "auth.login", nameof(StaffUser), user.Id.ToString());
        await db.SaveChangesAsync(cancellationToken);

        var token = tokenService.CreateAccessToken(user);
        return new LoginResult(
            LoginOutcome.Success,
            new LoginResponse(token.Token, token.ExpiresAt, ToResponse(user)));
    }

    public async Task<LoginResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await db.StaffUsers.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken)
            ?? throw new NotFoundException("User not found.");
        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new RequestValidationException("currentPassword", "The current password is not correct.");
        }
        if (request.NewPassword == request.CurrentPassword)
        {
            throw new RequestValidationException("newPassword", "Choose a different password.");
        }
        PasswordPolicy.Validate(request.NewPassword, user.Email);

        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        user.PasswordChangedAt = clock.GetUtcNow();
        user.MustChangePassword = false;
        // Signs out every other session of this user.
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        audit.Log("auth.password_changed", nameof(StaffUser), user.Id.ToString());
        await db.SaveChangesAsync(cancellationToken);
        sessions.Invalidate(user.Id);

        var token = tokenService.CreateAccessToken(user);
        return new LoginResponse(token.Token, token.ExpiresAt, ToResponse(user));
    }

    public async Task<CurrentUserResponse?> GetUserAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.StaffUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id && u.IsActive, cancellationToken);
        return user is null ? null : ToResponse(user);
    }

    private static CurrentUserResponse ToResponse(StaffUser user) =>
        new(user.Id, user.Email, user.FullName, user.Role, user.MustChangePassword);
}
