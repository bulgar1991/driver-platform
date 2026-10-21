using System.Net;
using System.Net.Http.Json;
using DriverPlatform.Application.Auth;
using DriverPlatform.Tests.Infrastructure;

namespace DriverPlatform.Tests.Auth;

public sealed class LockoutTests
{
    [Fact]
    public async Task Account_locks_after_too_many_wrong_passwords()
    {
        // Own factory: the lockout must not leak into other tests.
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        for (var i = 0; i < AuthService.MaxFailedLogins; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(ApiFactory.AdminEmail, "wrong"));
        }
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(ApiFactory.AdminEmail, ApiFactory.AdminPassword));

        Assert.Equal(HttpStatusCode.Locked, response.StatusCode);
    }
}
