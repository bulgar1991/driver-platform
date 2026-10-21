using System.Security.Claims;
using System.Text.Encodings.Web;
using DriverPlatform.Application.Partners;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DriverPlatform.Api.Extensions;

/// <summary>Partner API authentication: the key goes in the "X-Api-Key" header.</summary>
public sealed class PartnerApiKeyHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IPartnerAuthService partners) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "PartnerApiKey";
    public const string Header = "X-Api-Key";
    public const string PartnerIdClaim = "partner_id";
    public const string PartnerRole = "Partner";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Header, out var values) || string.IsNullOrWhiteSpace(values.ToString()))
        {
            return AuthenticateResult.NoResult();
        }
        var partner = await partners.AuthenticateAsync(values.ToString(), Context.RequestAborted);
        if (partner is null)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(PartnerIdClaim, partner.Id.ToString()),
                new Claim(ClaimTypes.Name, partner.Name),
                new Claim(ClaimTypes.Role, PartnerRole),
            ],
            SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = $"ApiKey header=\"{Header}\"";
        return Task.CompletedTask;
    }
}
