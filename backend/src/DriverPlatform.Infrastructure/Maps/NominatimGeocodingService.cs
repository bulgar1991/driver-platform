using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DriverPlatform.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DriverPlatform.Infrastructure.Maps;

/// <summary>
/// Address search on Nominatim (OpenStreetMap), restricted to the countries served (Maps:Countries).
/// Results are cached for a day.
/// </summary>
public sealed class NominatimGeocodingService(
    HttpClient http, IMemoryCache cache, IOptions<MapsOptions> options, ILogger<NominatimGeocodingService> logger)
    : IGeocodingService
{
    private readonly string[] _countries = options.Value.Countries.Select(c => c.ToLowerInvariant()).ToArray();

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

        var path = $"search?format=jsonv2&addressdetails=1&countrycodes={string.Join(',', _countries)}&limit=6&accept-language={Uri.EscapeDataString(language)}&q={Uri.EscapeDataString(query)}";
        var places = await SendAsync<List<NominatimPlace>>(path, cancellationToken);
        if (places is null)
        {
            return [];
        }

        IReadOnlyList<GeocodedAddress> result = places.Select(p => ToAddress(p, withName: true)).OfType<GeocodedAddress>().ToList();
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
        // A click outside the countries served (Austria, the sea…) is not an address we can use.
        var result = place is null || !_countries.Contains(place.Address?.CountryCode ?? "")
            ? null
            : ToAddress(place, withName: false);
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

    private static GeocodedAddress? ToAddress(NominatimPlace place, bool withName)
    {
        if (!double.TryParse(place.Lat, CultureInfo.InvariantCulture, out var lat)
            || !double.TryParse(place.Lon, CultureInfo.InvariantCulture, out var lon)
            || string.IsNullOrEmpty(place.DisplayName))
        {
            return null;
        }

        var a = place.Address;
        var parts = new AddressParts(
            Name: place.Name,
            Road: a?.Road ?? a?.Pedestrian ?? a?.Square,
            HouseNumber: a?.HouseNumber,
            Postcode: a?.Postcode,
            City: a?.City ?? a?.Town ?? a?.Village ?? a?.Municipality,
            // The most precise area first: "San Marco", not "Venezia-Murano-Burano".
            Suburb: a?.Neighbourhood ?? a?.Quarter ?? a?.Hamlet ?? a?.Suburb,
            Region: a?.State);
        var formatted = AddressFormatter.Format(parts, withName);
        return formatted is { } f
            ? new GeocodedAddress(f.Label, f.Address, lat, lon)
            : new GeocodedAddress(place.DisplayName, place.DisplayName, lat, lon);
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
        string? Municipality,
        string? Pedestrian,
        string? Square,
        string? Postcode,
        string? Suburb,
        string? Hamlet,
        string? Neighbourhood,
        string? Quarter,
        string? State,
        [property: JsonPropertyName("country_code")] string? CountryCode);
}
