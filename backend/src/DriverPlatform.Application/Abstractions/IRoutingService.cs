using DriverPlatform.Application.Geo;

namespace DriverPlatform.Application.Abstractions;

/// <param name="Path">Route shape as [latitude, longitude] pairs, for the map.</param>
/// <param name="IsEstimate">True when the map provider was unreachable and the distance is a straight-line estimate.</param>
public sealed record RouteResult(decimal DistanceKm, int DurationMinutes, IReadOnlyList<double[]> Path, bool IsEstimate);

public interface IRoutingService
{
    Task<RouteResult> GetRouteAsync(Coordinate from, Coordinate to, CancellationToken cancellationToken);
}
