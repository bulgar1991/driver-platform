using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DriverPlatform.Application.Auth;
using DriverPlatform.Application.Staff;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Infrastructure.Persistence;
using DriverPlatform.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DriverPlatform.Tests.Auth;

public sealed class TwoFactorTests
{
    // RFC 6238 appendix B (SHA-1 secret "12345678901234567890"), last six digits.
    [Theory]
    [InlineData(59, "287082")]
    [InlineData(1111111109, "081804")]
    [InlineData(1234567890, "005924")]
    [InlineData(2000000000, "279037")]
    public void Codes_match_the_rfc_test_vectors(long unixSeconds, string expected) =>
        Assert.Equal(expected, Totp.Code(Encoding.ASCII.GetBytes("12345678901234567890"), Totp.StepAt(DateTimeOffset.FromUnixTimeSeconds(unixSeconds))));

    [Fact]
    public void Base32_matches_rfc_4648() => Assert.Equal("MZXW6YTBOI", Base32.Encode("foobar"u8));

    [Fact]
    public void A_code_is_accepted_once_and_only_near_its_time()
    {
        var secret = Totp.NewSecret();
        var now = DateTimeOffset.UtcNow;
        var step = Totp.StepAt(now);
        Assert.Equal(step, Totp.Verify(secret, Totp.Code(secret, step), now, lastUsedStep: 0));
        Assert.Equal(step - 1, Totp.Verify(secret, Totp.Code(secret, step - 1), now, 0));
        Assert.Null(Totp.Verify(secret, Totp.Code(secret, step - 3), now, 0));
        Assert.Null(Totp.Verify(secret, Totp.Code(secret, step), now, lastUsedStep: step));
    }

    private sealed record Account(ApiFactory Factory, HttpClient Admin, byte[] Secret, IReadOnlyList<string> RecoveryCodes);

    /// <summary>The bootstrap admin turns two-step sign-in on, as from My account.</summary>
    private static async Task<Account> EnableAsync()
    {
        var factory = new ApiFactory { RequireTwoFactor = true };
        var admin = await factory.CreateAdminClientAsync();

        var setup = await Read<TwoFactorSetupResponse>(await admin.PostAsync("/api/auth/two-factor/setup", null));
        Assert.StartsWith("data:image/png;base64,", setup.QrCode);
        Assert.StartsWith("otpauth://totp/", setup.SetupUri);
        Assert.Contains("issuer=", setup.SetupUri);
        var secret = DecodeBase32(setup.Secret.Replace(" ", ""));

        var wrong = await admin.PostAsJsonAsync("/api/auth/two-factor/enable", new TwoFactorCodeRequest("000000"), TestData.Json);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var enabled = await Read<TwoFactorEnabledResponse>(await admin.PostAsJsonAsync("/api/auth/two-factor/enable",
            new TwoFactorCodeRequest(CodeNow(secret)), TestData.Json));
        Assert.Equal(10, enabled.RecoveryCodes.Count);
        Assert.All(enabled.RecoveryCodes, c => Assert.Matches("^[A-Z2-9]{4}-[A-Z2-9]{4}$", c));
        Assert.False(enabled.Session.User.TwoFactorSetupRequired);
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", enabled.Session.AccessToken);
        return new Account(factory, admin, secret, enabled.RecoveryCodes);
    }

    [Fact]
    public async Task Sign_in_asks_for_the_code_after_the_password()
    {
        var a = await EnableAsync();
        using var factory = a.Factory;
        var me = await Read<CurrentUserResponse>(await a.Admin.GetAsync("/api/auth/me"));
        Assert.True(me.TwoFactorEnabled);

        var client = a.Factory.CreateClient();
        var challenge = await ChallengeAsync(client);

        // The code used to turn it on cannot be used again.
        var replay = await client.PostAsJsonAsync("/api/auth/login/two-factor",
            new TwoFactorLoginRequest(challenge, CodeNow(a.Secret)), TestData.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Contains("two-factor-code", await replay.Content.ReadAsStringAsync());

        var login = await Read<LoginResponse>(await client.PostAsJsonAsync("/api/auth/login/two-factor",
            new TwoFactorLoginRequest(challenge, NextCode(a.Secret)), TestData.Json));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/staff")).StatusCode);

        // A forged or expired challenge gets nothing.
        var forged = await client.PostAsJsonAsync("/api/auth/login/two-factor",
            new TwoFactorLoginRequest(challenge[..^4] + "AAAA", NextCode(a.Secret)), TestData.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, forged.StatusCode);
        Assert.Contains("two-factor-expired", await forged.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Each_recovery_code_works_once()
    {
        var a = await EnableAsync();
        using var factory = a.Factory;
        var client = a.Factory.CreateClient();

        var first = await client.PostAsJsonAsync("/api/auth/login/two-factor",
            new TwoFactorLoginRequest(await ChallengeAsync(client), " " + a.RecoveryCodes[0].ToLowerInvariant().Replace("-", " ") + " "), TestData.Json);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var again = await client.PostAsJsonAsync("/api/auth/login/two-factor",
            new TwoFactorLoginRequest(await ChallengeAsync(client), a.RecoveryCodes[0]), TestData.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, again.StatusCode);

        var status = await Read<TwoFactorStatus>(await a.Admin.GetAsync("/api/auth/two-factor"));
        Assert.Equal(9, status.RecoveryCodesLeft);
    }

    [Fact]
    public async Task Wrong_codes_lock_the_account_like_wrong_passwords()
    {
        var a = await EnableAsync();
        using var factory = a.Factory;
        var client = a.Factory.CreateClient();
        var challenge = await ChallengeAsync(client);

        HttpResponseMessage last = null!;
        for (var i = 0; i < AuthService.MaxFailedLogins; i++)
        {
            last = await client.PostAsJsonAsync("/api/auth/login/two-factor", new TwoFactorLoginRequest(challenge, "111111"), TestData.Json);
        }
        Assert.Equal(HttpStatusCode.Locked, last.StatusCode);
        var right = await client.PostAsJsonAsync("/api/auth/login/two-factor", new TwoFactorLoginRequest(challenge, NextCode(a.Secret)), TestData.Json);
        Assert.Equal(HttpStatusCode.Locked, right.StatusCode);
    }

    [Fact]
    public async Task Turning_it_off_needs_the_password_and_a_code()
    {
        var a = await EnableAsync();
        using var factory = a.Factory;

        var noPassword = await a.Admin.PostAsJsonAsync("/api/auth/two-factor/disable",
            new TwoFactorDisableRequest("wrong-password-123", NextCode(a.Secret)), TestData.Json);
        Assert.Equal(HttpStatusCode.BadRequest, noPassword.StatusCode);

        var off = await a.Admin.PostAsJsonAsync("/api/auth/two-factor/disable",
            new TwoFactorDisableRequest(ApiFactory.AdminPassword, NextCode(a.Secret)), TestData.Json);
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        // Required: the new session opens only the setup again.
        Assert.True((await off.Content.ReadFromJsonAsync<LoginResponse>(TestData.Json))!.User.TwoFactorSetupRequired);

        var login = await Read<JsonElement>(await a.Factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest(ApiFactory.AdminEmail, ApiFactory.AdminPassword), TestData.Json));
        Assert.True(login.TryGetProperty("accessToken", out _));
    }

    [Fact]
    public async Task Until_the_app_is_set_up_only_my_account_opens()
    {
        using var factory = new ApiFactory { RequireTwoFactor = true };
        var admin = await factory.CreateAdminClientAsync();

        var me = await Read<CurrentUserResponse>(await admin.GetAsync("/api/auth/me"));
        Assert.True(me.TwoFactorSetupRequired);
        var blocked = await admin.GetAsync("/api/admin/staff");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Contains("two-step", await blocked.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync("/api/auth/two-factor/setup", null)).StatusCode);
    }

    [Fact]
    public async Task A_code_can_be_sent_by_email_instead()
    {
        var a = await EnableAsync();
        using var factory = a.Factory;
        var client = a.Factory.CreateClient();
        var challenge = await ChallengeAsync(client);
        a.Factory.Emails.Sent.Clear();

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/login/two-factor/email",
            new TwoFactorEmailRequest(challenge), TestData.Json)).StatusCode);
        // A second request within a minute sends nothing more.
        await client.PostAsJsonAsync("/api/auth/login/two-factor/email", new TwoFactorEmailRequest(challenge), TestData.Json);
        var mail = Assert.Single(a.Factory.Emails.Sent);
        Assert.Equal(ApiFactory.AdminEmail, mail.To);
        var code = System.Text.RegularExpressions.Regex.Match(mail.TextBody, @"\b\d{6}\b").Value;

        var login = await client.PostAsJsonAsync("/api/auth/login/two-factor", new TwoFactorLoginRequest(challenge, code), TestData.Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        // Used: the same email code does not work twice.
        var again = await client.PostAsJsonAsync("/api/auth/login/two-factor",
            new TwoFactorLoginRequest(await ChallengeAsync(client), code), TestData.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, again.StatusCode);

        var forged = await client.PostAsJsonAsync("/api/auth/login/two-factor/email", new TwoFactorEmailRequest("x.y"), TestData.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, forged.StatusCode);
    }

    [Fact]
    public async Task An_admin_can_reset_it_for_someone_who_lost_their_phone()
    {
        var a = await EnableAsync();
        using var factory = a.Factory;
        var other = await Read<TemporaryPasswordResponse>(await a.Admin.PostAsJsonAsync("/api/admin/staff",
            new CreateStaffRequest { Email = "second@admin.test", FullName = "Second Admin", Role = StaffRole.Admin }, TestData.Json),
            HttpStatusCode.Created);
        Guid adminId;
        using (var scope = a.Factory.Services.CreateScope())
        {
            adminId = await scope.ServiceProvider.GetRequiredService<AppDbContext>().StaffUsers
                .Where(u => u.Email == ApiFactory.AdminEmail).Select(u => u.Id).SingleAsync();
        }
        var list = await Read<List<StaffDto>>(await a.Admin.GetAsync("/api/admin/staff"));
        Assert.True(list.Single(s => s.Id == adminId).TwoFactorEnabled);

        var reset = await Read<StaffDto>(await a.Admin.PostAsync($"/api/admin/staff/{adminId}/reset-two-factor", null));
        Assert.False(reset.TwoFactorEnabled);
        Assert.NotNull(other.TemporaryPassword);

        // The reset ended the old sessions; signing in again opens only the setup.
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.Admin.GetAsync("/api/admin/staff")).StatusCode);
        var login = await Read<LoginResponse>(await a.Factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest(ApiFactory.AdminEmail, ApiFactory.AdminPassword), TestData.Json));
        Assert.True(login.User.TwoFactorSetupRequired);
    }

    private static async Task<string> ChallengeAsync(HttpClient client)
    {
        var response = await Read<JsonElement>(await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(ApiFactory.AdminEmail, ApiFactory.AdminPassword), TestData.Json));
        Assert.True(response.GetProperty("twoFactorRequired").GetBoolean());
        Assert.False(response.TryGetProperty("accessToken", out _));
        return response.GetProperty("challengeToken").GetString()!;
    }

    private static string CodeNow(byte[] secret) => Totp.Code(secret, Totp.StepAt(DateTimeOffset.UtcNow));

    /// <summary>The next 30-second code: newer than one already used, and still inside the accepted window.</summary>
    private static string NextCode(byte[] secret) => Totp.Code(secret, Totp.StepAt(DateTimeOffset.UtcNow) + 1);

    private static byte[] DecodeBase32(string text)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in text)
        {
            buffer = (buffer << 5) | alphabet.IndexOf(c);
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
            }
        }
        return [.. bytes];
    }

    private static async Task<T> Read<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.True(response.StatusCode == expected, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(TestData.Json))!;
    }
}
