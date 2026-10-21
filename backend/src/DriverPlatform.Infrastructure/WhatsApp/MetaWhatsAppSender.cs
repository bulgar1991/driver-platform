using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Domain.Enums;
using Microsoft.Extensions.Options;

namespace DriverPlatform.Infrastructure.WhatsApp;

/// <summary>WhatsApp Business Cloud API: template messages sent from the company's number.</summary>
public sealed class MetaWhatsAppSender(HttpClient http, IOptions<WhatsAppOptions> options) : IWhatsAppSender
{
    public async Task<string?> SendTemplateAsync(
        string phone, NotificationKind template, string language, IReadOnlyList<string> parameters, CancellationToken cancellationToken)
    {
        var o = options.Value;
        var body = new
        {
            messaging_product = "whatsapp",
            to = new string(phone.Where(char.IsDigit).ToArray()),
            type = "template",
            template = new
            {
                name = o.Templates[template],
                language = new { code = o.LanguageFor(language) },
                components = new[]
                {
                    new
                    {
                        type = "body",
                        // Template parameters may not contain new lines or tabs.
                        parameters = parameters.Select(p => new { type = "text", text = p.ReplaceLineEndings(" ").Replace('\t', ' ') }).ToArray(),
                    },
                },
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{o.ApiVersion}/{o.PhoneNumberId}/messages")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new("Bearer", o.AccessToken);
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"WhatsApp {(int)response.StatusCode}: {error}");
        }
        var result = await response.Content.ReadFromJsonAsync<SendResult>(cancellationToken);
        return result?.Messages?.FirstOrDefault()?.Id;
    }

    private sealed record SendResult([property: JsonPropertyName("messages")] List<SentMessage>? Messages);

    private sealed record SentMessage([property: JsonPropertyName("id")] string Id);
}
