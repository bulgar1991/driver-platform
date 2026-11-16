using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DriverPlatform.Application.BookingCom;
using DriverPlatform.Application.Common;
using Microsoft.Extensions.Options;

namespace DriverPlatform.Infrastructure.BookingCom;

/// <summary>
/// OAuth client credentials (Basic auth on the token endpoint, token valid one hour, reused until a
/// minute before it expires or until a 401), then the Supplier API with "Authorization: Bearer".
/// </summary>
public sealed class BookingComApiClient(HttpClient http, BookingComTokenCache tokens, IOptions<BookingComOptions> options)
    : IBookingComApi
{
    public const string HttpClientName = "booking-com";
    private const int PageSize = 500;
    private const int MaxPages = 50;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<IReadOnlyList<BcBooking>> ListAsync(DateTimeOffset pickupFrom, CancellationToken cancellationToken)
    {
        var from = Uri.EscapeDataString(pickupFrom.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));
        string? next = $"v1/bookings?size={PageSize}&pickUpDateFrom={from}";
        var seen = new HashSet<string>();
        var all = new List<BcBooking>();

        for (var page = 0; next is not null && page < MaxPages && seen.Add(next); page++)
        {
            var list = await SendAsync<BcBookingList>(HttpMethod.Get, next, null, cancellationToken);
            all.AddRange(list?.Bookings ?? []);
            next = list?.Links?.FirstOrDefault(l => l.Rel == "next")?.Href?.TrimStart('/');
        }
        return all;
    }

    public async Task<BcBooking> GetAsync(string customerReference, string bookingReference, CancellationToken cancellationToken) =>
        await SendAsync<BcBooking>(HttpMethod.Get, $"v1/bookings/{Esc(customerReference)}/{Esc(bookingReference)}", null, cancellationToken)
        ?? throw new BookingComApiException(404, "Booking.com returned an empty booking.");

    public Task RespondAsync(string customerReference, string bookingReference, bool accept, string stateHash, string? rejectReason,
        CancellationToken cancellationToken) =>
        SendAsync<JsonElement?>(HttpMethod.Post, $"v1/bookings/{Esc(customerReference)}/{Esc(bookingReference)}/responses", new
        {
            supplierResponse = accept ? "ACCEPT" : "REJECT",
            state_hash = stateHash,
            // Sending a reason with ACCEPT is a 400.
            cancellationReason = accept ? null : rejectReason ?? "OTHER",
        }, cancellationToken);

    public Task AssignDriverAsync(string bookingReference, BcDriverAssignment assignment, CancellationToken cancellationToken) =>
        SendAsync<JsonElement?>(HttpMethod.Post, $"v2/bookings/{Esc(bookingReference)}/assignDriver", assignment, cancellationToken);

    public Task SendDriverEventAsync(string customerReference, string bookingReference, string eventType, DateTimeOffset occurredAt,
        double? latitude, double? longitude, CancellationToken cancellationToken) =>
        SendAsync<JsonElement?>(HttpMethod.Post, $"v1/bookings/{Esc(customerReference)}/{Esc(bookingReference)}/driver/events", new
        {
            event_type = eventType,
            latitude = latitude is { } lat ? Math.Round(lat, 8) : (double?)null,
            longitude = longitude is { } lng ? Math.Round(lng, 8) : (double?)null,
            occurred_at = occurredAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
        }, cancellationToken);

    private async Task<T?> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, new Uri(new Uri(Base(options.Value.ApiUrl)), path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetAsync(http, attempt > 0, cancellationToken));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, body.GetType(), options: Json);
            }

            using var response = await http.SendAsync(request, cancellationToken);
            // An expired or revoked token: fetch a new one once.
            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                continue;
            }
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new BookingComApiException((int)response.StatusCode, Describe(response.StatusCode, text));
            }
            return string.IsNullOrWhiteSpace(text) || typeof(T) == typeof(JsonElement?)
                ? default
                : JsonSerializer.Deserialize<T>(text, Json);
        }
    }

    /// <summary>Booking.com errors are {"message", "description"}, some {"errorCode", "errorMessage"}.</summary>
    private static string Describe(HttpStatusCode status, string body)
    {
        try
        {
            var error = JsonSerializer.Deserialize<BcError>(body, Json);
            var detail = string.Join(": ", new[] { error?.Message, error?.Description, error?.ErrorMessage }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (detail.Length > 0)
            {
                return $"HTTP {(int)status} {detail}";
            }
        }
        catch (JsonException)
        {
        }
        return $"HTTP {(int)status} {(body.Length > 200 ? body[..200] : body)}".TrimEnd();
    }

    private static string Esc(string value) => Uri.EscapeDataString(value);

    internal static string Base(string url) => url.EndsWith('/') ? url : url + "/";

    private sealed record BcBookingList(List<BcLink>? Links, List<BcBooking>? Bookings);

    private sealed record BcLink(string? Rel, string? Href);

    private sealed record BcError(string? Message, string? Description, string? ErrorMessage);
}

/// <summary>One access token for the whole application (a singleton), shared by every request.</summary>
public sealed class BookingComTokenCache(IOptions<BookingComOptions> options, TimeProvider clock)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetAsync(HttpClient http, bool renew, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!renew && _token is not null && clock.GetUtcNow() < _expiresAt)
            {
                return _token;
            }
            var settings = options.Value;
            if (settings.ClientId.Length == 0 || settings.ClientSecret.Length == 0)
            {
                throw new BookingComApiException(401, "BookingCom:ClientId and BookingCom:ClientSecret are not set.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, settings.AuthUrl + (settings.AuthUrl.Contains('?') ? "&" : "?") + "grant_type=client_credentials")
            {
                Content = new FormUrlEncodedContent([new("grant_type", "client_credentials")]),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.ClientId}:{settings.ClientSecret}")));
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new BookingComApiException((int)response.StatusCode,
                    $"Booking.com sign-in failed (HTTP {(int)response.StatusCode}); check BookingCom:AuthUrl, ClientId and ClientSecret.");
            }
            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                ?? throw new BookingComApiException(502, "Booking.com sign-in returned no token.");
            _token = token.AccessToken;
            _expiresAt = clock.GetUtcNow().AddSeconds(Math.Max(60, token.ExpiresIn) - 60);
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
