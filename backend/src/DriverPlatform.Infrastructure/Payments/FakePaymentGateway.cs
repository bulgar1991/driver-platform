using System.Collections.Concurrent;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Domain.Enums;
using Microsoft.Extensions.Options;

namespace DriverPlatform.Infrastructure.Payments;

/// <summary>
/// Development stand-in for Stripe, used when Payments:Provider is "Fake". The payment page is the
/// frontend's /dev/payment screen; nothing is charged. Sessions live in memory only.
/// </summary>
public sealed class FakePaymentGateway(IOptions<PaymentsOptions> options) : IPaymentGateway
{
    public sealed class FakeSession
    {
        public required string Id { get; init; }
        public required CheckoutRequest Request { get; init; }
        public decimal Amount => Request.Lines.Sum(l => l.Amount);
        public PaymentMethod? PaidWith { get; set; }
    }

    private readonly ConcurrentDictionary<string, FakeSession> _sessions = new();
    private readonly ConcurrentDictionary<string, CheckoutSession> _byIdempotencyKey = new();

    public string Name => "Fake";

    public Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        var session = _byIdempotencyKey.GetOrAdd(request.IdempotencyKey, _ =>
        {
            var id = $"fake_cs_{Guid.NewGuid():N}";
            _sessions[id] = new FakeSession { Id = id, Request = request };
            return new CheckoutSession(id, $"{options.Value.FrontendBaseUrl.TrimEnd('/')}/dev/payment/{id}");
        });
        return Task.FromResult(session);
    }

    public Task<CheckoutDetails> GetCheckoutAsync(string sessionId, CancellationToken cancellationToken)
    {
        var session = Find(sessionId);
        return Task.FromResult(new CheckoutDetails(
            session.Id,
            Paid: session.PaidWith is not null,
            Expired: false,
            session.PaidWith is null ? null : $"fake_pi_{session.Id[8..]}",
            session.PaidWith,
            session.Amount));
    }

    public Task<RefundResult> RefundAsync(string paymentIntentId, decimal amount, string idempotencyKey, CancellationToken cancellationToken) =>
        Task.FromResult(new RefundResult($"fake_re_{idempotencyKey}", "succeeded"));

    public PaymentEvent? ParseWebhook(string payload, string signature) =>
        throw new InvalidWebhookException("The fake gateway does not receive webhooks.");

    public FakeSession Find(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var session) ? session : throw new NotFoundException("Payment session not found.");

    public void MarkPaid(string sessionId, PaymentMethod method) => Find(sessionId).PaidWith = method;
}
