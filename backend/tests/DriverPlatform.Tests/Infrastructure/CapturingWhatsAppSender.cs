using System.Collections.Concurrent;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Tests.Infrastructure;

public sealed class CapturingWhatsAppSender : IWhatsAppSender
{
    public sealed record Message(string Phone, NotificationKind Template, string Language, IReadOnlyList<string> Parameters);

    public ConcurrentQueue<Message> Sent { get; } = new();

    /// <summary>Phone numbers for which sending fails, to test the failure log.</summary>
    public HashSet<string> Failing { get; } = [];

    public Task<string?> SendTemplateAsync(
        string phone, NotificationKind template, string language, IReadOnlyList<string> parameters, CancellationToken cancellationToken)
    {
        if (Failing.Contains(phone))
        {
            throw new HttpRequestException("WhatsApp 400: recipient not on WhatsApp");
        }
        Sent.Enqueue(new Message(phone, template, language, parameters));
        return Task.FromResult<string?>($"wamid.test{Sent.Count}");
    }
}
