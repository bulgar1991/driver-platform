using Microsoft.AspNetCore.Authentication;

namespace DriverPlatform.Api.Extensions;

public static class PartnerApi
{
    /// <summary>OpenAPI document of the partner API, published at /api/partner/openapi/partner.json.</summary>
    public const string Document = "partner";
    public const string Policy = "Partner";

    public static IServiceCollection AddPartnerApi(this IServiceCollection services)
    {
        new AuthenticationBuilder(services)
            .AddScheme<AuthenticationSchemeOptions, PartnerApiKeyHandler>(PartnerApiKeyHandler.SchemeName, null);
        services.AddAuthorizationBuilder()
            .AddPolicy(Policy, p => p
                .AddAuthenticationSchemes(PartnerApiKeyHandler.SchemeName)
                .RequireRole(PartnerApiKeyHandler.PartnerRole));
        services.AddOpenApi(Document, o =>
        {
            // Only the partner endpoints; everything else stays out of the public description.
            o.ShouldInclude = description => description.GroupName == Document;
            o.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "Magnus Drive partner API";
                document.Info.Version = "v1";
                document.Info.Description =
                    "Send transfer bookings in Italy, France, Germany and Switzerland. Authenticate with the X-Api-Key header. " +
                    "Status changes are POSTed to your webhook URL, signed with MagnusDrive-Signature.";
                return Task.CompletedTask;
            });
        });
        return services;
    }
}
