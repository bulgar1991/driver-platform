using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Geo;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DriverPlatform.Infrastructure.Maps;

/// <summary>
/// Driving distance and duration from OSRM. Routes are cached for an hour, so the booking uses the same distance
/// as the quote the client saw. When OSRM is down the distance is estimated and flagged.
/// </summary>
public sealed class OsrmRoutingService(HttpClient http, IMemoryCache cache, ILogger<OsrmRoutingService> logger) : IRoutingService
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(1);

    public async Task<RouteResult> GetRouteAsync(Coordinate from, Coordinate to, CancellationToken cancellationToken)
    {
        var key = FormattableString.Invariant($"route:{from.Latitude:F5},{from.Longitude:F5}:{to.Latitude:F5},{to.Longitude:F5}");
        if (cache.TryGetValue(key, out RouteResult? cached) && cached is not null)
        {
            return cached;
        }

        var route = await FetchAsync(from, to, cancellationToken);
        if (route is null)
        {
            var estimate = GeoMath.EstimateRoute(from, to);
            // Not cached, so the next request tries OSRM again.
            return new RouteResult(estimate.DistanceKm, estimate.DurationMinutes,
                [[from.Latitude, from.Longitude], [to.Latitude, to.Longitude]], IsEstimate: true);
        }

        cache.Set(key, route, CacheFor);
        return route;
    }

    private async Task<RouteResult?> FetchAsync(Coordinate from, Coordinate to, CancellationToken cancellationToken)
    {
        var path = string.Create(CultureInfo.InvariantCulture,
            $"route/v1/driving/{from.Longitude},{from.Latitude};{to.Longitude},{to.Latitude}?overview=simplified&geometries=geojson");
        try
        {
            var body = await http.GetFromJsonAsync<OsrmResponse>(path, cancellationToken);
            var route = body?.Routes?.FirstOrDefault();
            if (body?.Code != "Ok" || route is null)
            {
                logger.LogWarning("OSRM returned {Code} for {Path}", body?.Code, path);
                return null;
            }

            // GeoJSON is [longitude, latitude]; the map wants [latitude, longitude].
            var shape = route.Geometry?.Coordinates?.Select(c => new[] { c[1], c[0] }).ToList() ?? [];
            return new RouteResult(
                (decimal)route.Distance / 1000,
                (int)Math.Ceiling(route.Duration / 60),
                shape,
                IsEstimate: false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            logger.LogWarning(ex, "OSRM unavailable; using a straight-line estimate");
            return null;
        }
    }

    private sealed record OsrmResponse(string? Code, List<OsrmRoute>? Routes);

    private sealed record OsrmRoute(double Distance, double Duration, OsrmGeometry? Geometry);

    private sealed record OsrmGeometry([property: JsonPropertyName("coordinates")] List<double[]>? Coordinates);
}
