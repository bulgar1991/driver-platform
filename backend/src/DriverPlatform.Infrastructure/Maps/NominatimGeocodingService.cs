using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DriverPlatform.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DriverPlatform.Infrastructure.Maps;

/// <summary>Address search on Nominatim (OpenStreetMap), restricted to Italy. Results are cached for a day.</summary>
public sealed class NominatimGeocodingService(HttpClient http, IMemoryCache cache, ILogger<NominatimGeocodingService> logger)
    : IGeocodingService
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromDays(1);
    // The public server allows one request per second for the whole application.
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTimeOffset _lastRequest = DateTimeOffset.MinValue;

    public async Task<IReadOnlyList<GeocodedAddress>> SearchAsync(string query, string language, CancellationToken cancellationToken)
    {
        var key = $"geo:search:{language}:{query.ToLowerInvariant()}";
        if (cache.TryGetValue(key, out IReadOnlyList<GeocodedAddress>? cached) && cached is not null)
        {
            return cached;
        }

        var path = $"search?format=jsonv2&addressdetails=1&countrycodes=it&limit=6&accept-language={Uri.EscapeDataString(language)}&q={Uri.EscapeDataString(query)}";
        var places = await SendAsync<List<NominatimPlace>>(path, cancellationToken);
        if (places is null)
        {
            return [];
        }

        IReadOnlyList<GeocodedAddress> result = places.Select(ToAddress).OfType<GeocodedAddress>().ToList();
        cache.Set(key, result, CacheFor);
        return result;
    }

    public async Task<GeocodedAddress?> ReverseAsync(double latitude, double longitude, string language, CancellationToken cancellationToken)
    {
        var key = FormattableString.Invariant($"geo:reverse:{language}:{latitude:F5},{longitude:F5}");
        if (cache.TryGetValue(key, out GeocodedAddress? cached))
        {
            return cached;
        }

        var path = string.Create(CultureInfo.InvariantCulture,
            $"reverse?format=jsonv2&addressdetails=1&zoom=18&accept-language={Uri.EscapeDataString(language)}&lat={latitude}&lon={longitude}");
        var place = await SendAsync<NominatimPlace>(path, cancellationToken);
        var result = place is null ? null : ToAddress(place);
        if (result is not null)
        {
            cache.Set(key, result, CacheFor);
        }
        return result;
    }

    private async Task<T?> SendAsync<T>(string path, CancellationToken cancellationToken) where T : class
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var wait = _lastRequest.AddSeconds(1) - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, cancellationToken);
            }
            _lastRequest = DateTimeOffset.UtcNow;
            return await http.GetFromJsonAsync<T>(path, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            logger.LogWarning(ex, "Nominatim unavailable");
            return null;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static GeocodedAddress? ToAddress(NominatimPlace place)
    {
        if (!double.TryParse(place.Lat, CultureInfo.InvariantCulture, out var lat)
            || !double.TryParse(place.Lon, CultureInfo.InvariantCulture, out var lon)
            || string.IsNullOrEmpty(place.DisplayName))
        {
            return null;
        }

        var a = place.Address;
        var street = string.Join(" ", new[] { a?.Road, a?.HouseNumber }.Where(s => !string.IsNullOrEmpty(s)));
        var city = a?.City ?? a?.Town ?? a?.Village ?? a?.Municipality;
        var label = string.IsNullOrEmpty(place.Name) || place.Name == a?.Road
            ? string.Join(", ", new[] { street, city }.Where(s => !string.IsNullOrEmpty(s)))
            : string.Join(", ", new[] { place.Name, city }.Where(s => !string.IsNullOrEmpty(s)));
        return new GeocodedAddress(string.IsNullOrEmpty(label) ? place.DisplayName : label, place.DisplayName, lat, lon);
    }

    private sealed record NominatimPlace(
        string? Lat,
        string? Lon,
        string? Name,
        [property: JsonPropertyName("display_name")] string? DisplayName,
        NominatimAddress? Address);

    private sealed record NominatimAddress(
        string? Road,
        [property: JsonPropertyName("house_number")] string? HouseNumber,
        string? City,
        string? Town,
        string? Village,
        string? Municipality);
}
