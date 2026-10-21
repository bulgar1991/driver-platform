using System.Text.Json;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Domain.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DriverPlatform.Infrastructure.WhatsApp;

/// <summary>Development stand-in until the WhatsApp Business account is approved: one JSON line per message.</summary>
public sealed class LogWhatsAppSender(
    IOptions<WhatsAppOptions> options,
    IHostEnvironment environment,
    ILogger<LogWhatsAppSender> logger) : IWhatsAppSender
{
    private static readonly SemaphoreSlim FileGate = new(1, 1);

    public async Task<string?> SendTemplateAsync(
        string phone, NotificationKind template, string language, IReadOnlyList<string> parameters, CancellationToken cancellationToken)
    {
        var o = options.Value;
        var id = $"log_{Guid.NewGuid():N}";
        var line = JsonSerializer.Serialize(new
        {
            id,
            at = DateTimeOffset.UtcNow,
            to = phone,
            template = o.Templates[template],
            language = o.LanguageFor(language),
            parameters,
        });
        logger.LogInformation("WhatsApp (not sent, log provider) {Line}", line);

        var folder = Path.GetFullPath(o.LogDirectory, environment.ContentRootPath);
        Directory.CreateDirectory(folder);
        await FileGate.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(Path.Combine(folder, $"{DateTime.UtcNow:yyyy-MM-dd}.jsonl"), line + "\n", cancellationToken);
        }
        finally
        {
            FileGate.Release();
        }
        return id;
    }
}
