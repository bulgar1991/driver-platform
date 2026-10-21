using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DriverPlatform.Application.Auth;
using DriverPlatform.Application.Staff;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Tests.Infrastructure;

namespace DriverPlatform.Tests.Launch;

public sealed class StaffSecurityTests
{
    private static async Task<(HttpClient Client, LoginResponse Login)> SignInAsync(ApiFactory factory, string email, string password)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var login = (await response.Content.ReadFromJsonAsync<LoginResponse>(TestData.Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return (client, login);
    }

    [Fact]
    public async Task New_dispatcher_must_replace_the_temporary_password_before_using_the_panel()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();

        var created = await admin.PostAsJsonAsync("/api/admin/staff",
            new CreateStaffRequest { Email = "Lucia@Magnus.test", FullName = "Lucia Romano" }, TestData.Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var account = (await created.Content.ReadFromJsonAsync<TemporaryPasswordResponse>(TestData.Json))!;
        Assert.Equal(16, account.TemporaryPassword.Length);
        Assert.Equal(StaffRole.Dispatcher, account.User.Role);

        var (lucia, login) = await SignInAsync(factory, "lucia@magnus.test", account.TemporaryPassword);
        Assert.True(login.User.MustChangePassword);
        Assert.Equal(HttpStatusCode.Forbidden, (await lucia.GetAsync("/api/admin/bookings")).StatusCode);

        var weak = await lucia.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest(account.TemporaryPassword, "short"), TestData.Json);
        var guessable = await lucia.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest(account.TemporaryPassword, "lucia-password-2026"), TestData.Json);
        var changed = await lucia.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest(account.TemporaryPassword, "gondola at sunset over rialto"), TestData.Json);
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, guessable.StatusCode);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var fresh = (await changed.Content.ReadFromJsonAsync<LoginResponse>(TestData.Json))!;
        Assert.False(fresh.User.MustChangePassword);

        // The temporary-password token no longer works; the new one opens the panel.
        Assert.Equal(HttpStatusCode.Unauthorized, (await lucia.GetAsync("/api/admin/bookings")).StatusCode);
        lucia.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fresh.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await lucia.GetAsync("/api/admin/bookings")).StatusCode);
        // Dispatchers do not manage staff.
        Assert.Equal(HttpStatusCode.Forbidden, (await lucia.GetAsync("/api/admin/staff")).StatusCode);
    }

    [Fact]
    public async Task Deactivating_an_account_ends_its_open_session()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();
        var dispatcher = await factory.CreateDispatcherClientAsync();
        Assert.Equal(HttpStatusCode.OK, (await dispatcher.GetAsync("/api/admin/bookings")).StatusCode);

        var staff = await admin.GetFromJsonAsync<List<StaffDto>>("/api/admin/staff", TestData.Json);
        var target = staff!.Single(s => s.Role == StaffRole.Dispatcher);
        (await admin.PutAsJsonAsync($"/api/admin/staff/{target.Id}",
            new UpdateStaffRequest { FullName = target.FullName, Role = target.Role, IsActive = false }, TestData.Json)).EnsureSuccessStatusCode();

        // The session cache is cleared when the account changes, so the rejection is immediate.
        Assert.Equal(HttpStatusCode.Unauthorized, (await dispatcher.GetAsync("/api/admin/bookings")).StatusCode);
    }

    [Fact]
    public async Task The_last_admin_cannot_be_removed()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();
        var me = (await admin.GetFromJsonAsync<List<StaffDto>>("/api/admin/staff", TestData.Json))!.Single(s => s.Role == StaffRole.Admin);

        var demote = await admin.PutAsJsonAsync($"/api/admin/staff/{me.Id}",
            new UpdateStaffRequest { FullName = me.FullName, Role = StaffRole.Dispatcher }, TestData.Json);

        Assert.Equal(HttpStatusCode.Conflict, demote.StatusCode);
    }

    [Fact]
    public async Task Password_reset_unlocks_and_issues_a_temporary_password()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();
        var created = (await (await admin.PostAsJsonAsync("/api/admin/staff",
            new CreateStaffRequest { Email = "marco@magnus.test", FullName = "Marco" }, TestData.Json)).Content.ReadFromJsonAsync<TemporaryPasswordResponse>(TestData.Json))!;
        for (var i = 0; i < 5; i++)
        {
            await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("marco@magnus.test", "wrong"));
        }
        Assert.Equal(HttpStatusCode.Locked, (await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("marco@magnus.test", created.TemporaryPassword))).StatusCode);

        var reset = (await (await admin.PostAsync($"/api/admin/staff/{created.User.Id}/reset-password", null)).Content.ReadFromJsonAsync<TemporaryPasswordResponse>(TestData.Json))!;

        Assert.NotEqual(created.TemporaryPassword, reset.TemporaryPassword);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("marco@magnus.test", reset.TemporaryPassword))).StatusCode);
        var audit = await admin.GetFromJsonAsync<AuditPage>("/api/admin/audit?search=staff.", TestData.Json);
        Assert.Contains(audit!.Items, a => a.Action == "staff.password_reset" && a.Actor == ApiFactory.AdminEmail);
    }

    [Fact]
    public async Task Api_responses_carry_security_headers()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateClient().GetAsync("/api/booking-options");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }
}
