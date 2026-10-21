using System.Collections.Concurrent;
using DriverPlatform.Application.Abstractions;

namespace DriverPlatform.Tests.Infrastructure;

public sealed class CapturingEmailSender : IEmailSender
{
    public ConcurrentQueue<EmailMessage> Sent { get; } = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Sent.Enqueue(message);
        return Task.CompletedTask;
    }
}
