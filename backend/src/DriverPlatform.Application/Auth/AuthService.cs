using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using DriverPlatform.Application.Localization;

namespace DriverPlatform.Application.Auth;

public sealed class AuthService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IAuditLogger audit,
    IStaffSessions sessions,
    TwoFactorKeys twoFactor,
    IQrCodeRenderer qrCodes,
    IOptions<CompanyOptions> company,
    IOptions<TwoFactorOptions> twoFactorOptions,
    IEmailSender email,
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
            await FailAsync(user, now, cancellationToken);
            return new LoginResult(LoginOutcome.InvalidCredentials);
        }

        if (user.TwoFactorEnabledAt is not null)
        {
            // Failed attempts are not reset yet: wrong codes count towards the same lockout.
            var expiresAt = now.Add(TwoFactorKeys.ChallengeLifetime);
            return new LoginResult(LoginOutcome.TwoFactorRequired,
                Challenge: new TwoFactorChallengeResponse(twoFactor.CreateChallenge(user.Id, user.SecurityStamp, expiresAt), expiresAt));
        }

        return await SucceedAsync(user, now, "password", cancellationToken);
    }

    public async Task<LoginResult> LoginTwoFactorAsync(TwoFactorLoginRequest request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var user = await ChallengedUserAsync(request.ChallengeToken, now, cancellationToken);
        if (user is null)
        {
            return new LoginResult(LoginOutcome.ChallengeExpired);
        }
        if (user.LockedUntil > now)
        {
            return new LoginResult(LoginOutcome.LockedOut);
        }

        var method = CheckCode(user, request.Code ?? "", now);
        if (method is null)
        {
            await FailAsync(user, now, cancellationToken);
            return new LoginResult(user.LockedUntil > now ? LoginOutcome.LockedOut : LoginOutcome.InvalidCode);
        }
        return await SucceedAsync(user, now, method, cancellationToken);
    }

    public async Task<bool> SendEmailCodeAsync(TwoFactorEmailRequest request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var user = await ChallengedUserAsync(request.ChallengeToken, now, cancellationToken);
        if (user is null)
        {
            return false;
        }
        if (user.TwoFactorEmailCodeSentAt > now.AddMinutes(-1) || user.LockedUntil > now)
        {
            return true;
        }
        var minutes = twoFactorOptions.Value.EmailCodeMinutes;
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var firstName = user.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? user.FullName;
        var text = Texts.Current.TwoFactorEmailBody(firstName, code, minutes);
        var html = "<p>" + WebUtility.HtmlEncode(text).Replace("\n\n", "</p><p>").Replace(code, $"<strong style=\"font-size:22px;letter-spacing:4px\">{code}</strong>") + "</p>";
        try
        {
            await email.SendAsync(new EmailMessage(user.Email, user.FullName, Texts.Current.TwoFactorEmailSubject(company.Value.Name),
                html, text, []), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ConflictException(Texts.Current.TwoFactorEmailFailed);
        }
        user.TwoFactorEmailCodeHash = twoFactor.HashEmailCode(user.Id, code);
        user.TwoFactorEmailCodeExpiresAt = now.AddMinutes(minutes);
        user.TwoFactorEmailCodeSentAt = now;
        audit.LogAs(user.Email, "auth.email_code_sent", nameof(StaffUser), user.Id.ToString());
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<LoginResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await ActiveUserAsync(userId, cancellationToken);
        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new RequestValidationException("currentPassword", Texts.Current.CurrentPasswordWrong);
        }
        if (request.NewPassword == request.CurrentPassword)
        {
            throw new RequestValidationException("newPassword", Texts.Current.ChooseDifferentPassword);
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

        return Session(user);
    }

    public async Task<CurrentUserResponse?> GetUserAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.StaffUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id && u.IsActive, cancellationToken);
        return user is null ? null : ToResponse(user);
    }

    public async Task<TwoFactorStatus> TwoFactorStatusAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await ActiveUserAsync(userId, cancellationToken);
        return new TwoFactorStatus(user.TwoFactorEnabledAt is not null, user.TwoFactorEnabledAt, user.TwoFactorRecoveryCodes.Count);
    }

    public async Task<TwoFactorSetupResponse> SetUpTwoFactorAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await ActiveUserAsync(userId, cancellationToken);
        if (user.TwoFactorEnabledAt is not null)
        {
            throw new ConflictException(Texts.Current.TwoFactorAlreadyOn);
        }
        var secret = Totp.NewSecret();
        user.TwoFactorPendingSecret = twoFactor.Protect(secret);
        await db.SaveChangesAsync(cancellationToken);

        var uri = Totp.SetupUri(company.Value.Name, user.Email, secret);
        var key = string.Join(' ', Base32.Encode(secret).Chunk(4).Select(c => new string(c)));
        return new TwoFactorSetupResponse(key, uri, qrCodes.PngDataUri(uri));
    }

    public async Task<TwoFactorEnabledResponse> EnableTwoFactorAsync(Guid userId, TwoFactorCodeRequest request, CancellationToken cancellationToken)
    {
        var user = await ActiveUserAsync(userId, cancellationToken);
        if (user.TwoFactorEnabledAt is not null)
        {
            throw new ConflictException(Texts.Current.TwoFactorAlreadyOn);
        }
        if (user.TwoFactorPendingSecret is null)
        {
            throw new ConflictException(Texts.Current.TwoFactorStartAgain);
        }
        var now = clock.GetUtcNow();
        var step = Totp.Verify(twoFactor.Unprotect(user.TwoFactorPendingSecret), Digits(request.Code), now, lastUsedStep: 0);
        if (step is null)
        {
            throw new RequestValidationException("code", Texts.Current.TwoFactorWrongCode);
        }

        var codes = TwoFactorKeys.NewRecoveryCodes();
        user.TwoFactorSecret = user.TwoFactorPendingSecret;
        user.TwoFactorPendingSecret = null;
        user.TwoFactorEnabledAt = now;
        user.TwoFactorLastStep = step.Value;
        user.TwoFactorRecoveryCodes = codes.Select(TwoFactorKeys.HashRecoveryCode).ToList();
        audit.Log("auth.two_factor_enabled", nameof(StaffUser), user.Id.ToString());
        await db.SaveChangesAsync(cancellationToken);
        return new TwoFactorEnabledResponse(codes, Session(user));
    }

    public async Task<LoginResponse> DisableTwoFactorAsync(Guid userId, TwoFactorDisableRequest request, CancellationToken cancellationToken)
    {
        var user = await ActiveUserAsync(userId, cancellationToken);
        if (user.TwoFactorEnabledAt is null)
        {
            return Session(user);
        }
        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new RequestValidationException("password", Texts.Current.CurrentPasswordWrong);
        }
        if (CheckCode(user, request.Code, clock.GetUtcNow()) is null)
        {
            throw new RequestValidationException("code", Texts.Current.TwoFactorWrongCode);
        }
        TurnOff(user);
        audit.Log("auth.two_factor_disabled", nameof(StaffUser), user.Id.ToString());
        await db.SaveChangesAsync(cancellationToken);
        sessions.Invalidate(user.Id);
        return Session(user);
    }

    public async Task<RecoveryCodesResponse> NewRecoveryCodesAsync(Guid userId, TwoFactorCodeRequest request, CancellationToken cancellationToken)
    {
        var user = await ActiveUserAsync(userId, cancellationToken);
        if (user.TwoFactorEnabledAt is null)
        {
            throw new ConflictException(Texts.Current.TwoFactorIsOff);
        }
        if (CheckCode(user, request.Code, clock.GetUtcNow(), allowRecoveryCode: false) is null)
        {
            throw new RequestValidationException("code", Texts.Current.TwoFactorWrongCode);
        }
        var codes = TwoFactorKeys.NewRecoveryCodes();
        user.TwoFactorRecoveryCodes = codes.Select(TwoFactorKeys.HashRecoveryCode).ToList();
        audit.Log("auth.recovery_codes_renewed", nameof(StaffUser), user.Id.ToString());
        await db.SaveChangesAsync(cancellationToken);
        return new RecoveryCodesResponse(codes);
    }

    /// <summary>
    /// Clears every two-step setting (also used by admins for someone who lost their phone) and ends the
    /// user's sessions, so a required setup is asked at once.
    /// </summary>
    public static void TurnOff(StaffUser user)
    {
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.TwoFactorEmailCodeHash = null;
        user.TwoFactorEmailCodeExpiresAt = null;
        user.TwoFactorEnabledAt = null;
        user.TwoFactorSecret = null;
        user.TwoFactorPendingSecret = null;
        user.TwoFactorLastStep = 0;
        user.TwoFactorRecoveryCodes = [];
    }

    /// <summary>"totp" or "recovery code" when the code is right (and records it as used), else null.</summary>
    private string? CheckCode(StaffUser user, string code, DateTimeOffset now, bool allowRecoveryCode = true)
    {
        if (user.TwoFactorSecret is null)
        {
            return null;
        }
        // Only digits (spaces allowed, "123 456"): the app's code. Anything else: a recovery code,
        // even one that happens to contain six digits.
        var digits = new string(code.Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (digits.Length == Totp.Digits && digits.All(char.IsAsciiDigit))
        {
            var step = Totp.Verify(twoFactor.Unprotect(user.TwoFactorSecret), digits, now, user.TwoFactorLastStep);
            if (step is not null)
            {
                user.TwoFactorLastStep = step.Value;
                return "totp";
            }
            if (user.TwoFactorEmailCodeHash is not null && user.TwoFactorEmailCodeExpiresAt > now
                && CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.ASCII.GetBytes(twoFactor.HashEmailCode(user.Id, digits)),
                    System.Text.Encoding.ASCII.GetBytes(user.TwoFactorEmailCodeHash)))
            {
                user.TwoFactorEmailCodeHash = null;
                user.TwoFactorEmailCodeExpiresAt = null;
                return "email code";
            }
            return null;
        }
        if (!allowRecoveryCode)
        {
            return null;
        }
        var hash = TwoFactorKeys.HashRecoveryCode(code);
        if (!user.TwoFactorRecoveryCodes.Contains(hash))
        {
            return null;
        }
        // Each recovery code works once; a new list is assigned so the change is saved.
        user.TwoFactorRecoveryCodes = user.TwoFactorRecoveryCodes.Where(h => h != hash).ToList();
        return "recovery code";
    }

    private async Task FailAsync(StaffUser user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        user.FailedLoginCount++;
        if (user.FailedLoginCount >= MaxFailedLogins)
        {
            user.LockedUntil = now.Add(LockoutDuration);
            user.FailedLoginCount = 0;
            audit.LogAs(user.Email, "auth.locked_out", nameof(StaffUser), user.Id.ToString());
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<LoginResult> SucceedAsync(StaffUser user, DateTimeOffset now, string method, CancellationToken cancellationToken)
    {
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;
        audit.LogAs(user.Email, "auth.login", nameof(StaffUser), user.Id.ToString(),
            method == "password" ? null : new { method, recoveryCodesLeft = user.TwoFactorRecoveryCodes.Count });
        await db.SaveChangesAsync(cancellationToken);

        return new LoginResult(LoginOutcome.Success, Session(user));
    }

    private LoginResponse Session(StaffUser user)
    {
        var token = tokenService.CreateAccessToken(user);
        return new LoginResponse(token.Token, token.ExpiresAt, ToResponse(user));
    }

    /// <summary>The user behind a valid challenge, if still active and with two-step sign-in on.</summary>
    private async Task<StaffUser?> ChallengedUserAsync(string? challengeToken, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (twoFactor.ReadChallenge(challengeToken ?? "", now) is not { } c)
        {
            return null;
        }
        var user = await db.StaffUsers.FirstOrDefaultAsync(u => u.Id == c.UserId, cancellationToken);
        return user is { IsActive: true, TwoFactorEnabledAt: not null } && user.SecurityStamp == c.SecurityStamp ? user : null;
    }

    private async Task<StaffUser> ActiveUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.StaffUsers.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken)
        ?? throw new NotFoundException(Texts.Current.UserNotFound);

    private static string Digits(string? code) => new((code ?? "").Where(char.IsDigit).ToArray());

    private CurrentUserResponse ToResponse(StaffUser user) =>
        new(user.Id, user.Email, user.FullName, user.Role, user.MustChangePassword, user.TwoFactorEnabledAt is not null,
            twoFactorOptions.Value.Required && user.TwoFactorEnabledAt is null);
}
