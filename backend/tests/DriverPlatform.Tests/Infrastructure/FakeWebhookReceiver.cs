using System.Collections.Concurrent;
using System.Net;

namespace DriverPlatform.Tests.Infrastructure;

/// <summary>Stands in for a partner's webhook endpoint.</summary>
public sealed class FakeWebhookReceiver : HttpMessageHandler
{
    public sealed record Received(Uri Url, string Body, string Signature, string Event);

    public ConcurrentQueue<Received> Requests { get; } = new();
    public HttpStatusCode Answer { get; set; } = HttpStatusCode.OK;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Enqueue(new Received(
            request.RequestUri!,
            await request.Content!.ReadAsStringAsync(cancellationToken),
            request.Headers.GetValues("Webhook-Signature").Single(),
            request.Headers.GetValues("Webhook-Event").Single()));
        return new HttpResponseMessage(Answer);
    }
}
