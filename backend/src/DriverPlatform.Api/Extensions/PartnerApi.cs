using DriverPlatform.Domain.Enums;
using Microsoft.Extensions.Options;
using DriverPlatform.Application.Common;
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
            o.AddDocumentTransformer((document, context, _) =>
            {
                var company = context.ApplicationServices.GetRequiredService<IOptions<CompanyOptions>>().Value;
                document.Info.Title = $"{company.Name} partner API";
                document.Info.Version = "v1";
                document.Info.Description =
                    "Send transfer bookings in the countries we serve. Authenticate with the X-Api-Key header. " +
                    "Status changes are POSTed to your webhook URL, signed with the Webhook-Signature header.";
                return Task.CompletedTask;
            });
            // A tariff class is a plain code ("Sedan", or one added in the panel), not an object.
            o.AddSchemaTransformer((schema, context, _) =>
            {
                if (context.JsonTypeInfo.Type == typeof(VehicleCategory))
                {
                    schema.Type = Microsoft.OpenApi.JsonSchemaType.String;
                    schema.Pattern = "^[A-Za-z][A-Za-z0-9]{0,31}$";
                    schema.Description =
                        "Vehicle class code: Sedan (Standard), Business, Estate (VIP), Van (Bus), or a class the company " +
                        "added. GET /api/booking-options lists the classes offered now.";
                    schema.Properties?.Clear();
                }
                return Task.CompletedTask;
            });
        });
        return services;
    }
}
