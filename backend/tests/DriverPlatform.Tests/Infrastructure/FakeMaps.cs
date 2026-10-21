using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Geo;

namespace DriverPlatform.Tests.Infrastructure;

/// <summary>Deterministic routing for tests: the straight-line estimate, never the network.</summary>
public sealed class FakeRoutingService : IRoutingService
{
    public List<(Coordinate From, Coordinate To)> Calls { get; } = [];

    public Task<RouteResult> GetRouteAsync(Coordinate from, Coordinate to, CancellationToken cancellationToken)
    {
        Calls.Add((from, to));
        var estimate = GeoMath.EstimateRoute(from, to);
        return Task.FromResult(new RouteResult(
            estimate.DistanceKm, estimate.DurationMinutes,
            [[from.Latitude, from.Longitude], [to.Latitude, to.Longitude]], IsEstimate: false));
    }
}

public sealed class FakeGeocodingService : IGeocodingService
{
    public Task<IReadOnlyList<GeocodedAddress>> SearchAsync(string query, string language, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<GeocodedAddress>>(
            [new GeocodedAddress($"{query}, Roma", $"{query}, 00100 Roma RM, Italia", 41.9, 12.5)]);

    public Task<GeocodedAddress?> ReverseAsync(double latitude, double longitude, string language, CancellationToken cancellationToken) =>
        Task.FromResult<GeocodedAddress?>(new GeocodedAddress("Via del Corso 1, Roma", "Via del Corso 1, 00186 Roma RM, Italia", latitude, longitude));
}
