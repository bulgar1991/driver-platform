using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DriverPlatform.Application.Auth;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Tests.Infrastructure;

namespace DriverPlatform.Tests.Auth;

public sealed class AuthApiTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ApiFactory _factory;

    public AuthApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_with_valid_credentials_returns_a_token_that_opens_me()
    {
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(ApiFactory.AdminEmail.ToUpperInvariant(), ApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>(Json);

        Assert.NotNull(body);
        Assert.Equal(StaffRole.Admin, body.User.Role);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);
        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me", Json);
        Assert.Equal(ApiFactory.AdminEmail, me?.Email);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("nobody@test.local", "wrong"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_without_token_returns_401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_is_public()
    {
        var response = await _factory.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Each_user_keeps_their_own_menu_order()
    {
        var client = await _factory.CreateDispatcherClientAsync();

        var saved = await client.PutAsJsonAsync("/api/auth/menu",
            new MenuOrderRequest { Order = ["/admin/calendar", "/admin", "https://evil.example", "/admin/calendar", ""] });
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);

        // Only panel pages, each once, in the order given.
        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me", Json);
        Assert.Equal(["/admin/calendar", "/admin"], me!.MenuOrder);

        // The admin's menu is untouched.
        var admin = await _factory.CreateAdminClientAsync();
        Assert.Empty((await admin.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me", Json))!.MenuOrder!);

        // An empty list goes back to the built-in order.
        await client.PutAsJsonAsync("/api/auth/menu", new MenuOrderRequest());
        Assert.Empty((await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me", Json))!.MenuOrder!);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _factory.CreateClient().PutAsJsonAsync("/api/auth/menu", new MenuOrderRequest())).StatusCode);
    }
}
