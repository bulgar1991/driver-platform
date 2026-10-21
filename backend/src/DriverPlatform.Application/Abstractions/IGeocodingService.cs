namespace DriverPlatform.Application.Abstractions;

public sealed record GeocodedAddress(string Label, string Address, double Latitude, double Longitude);

/// <summary>Address search on the map provider, limited to Italy.</summary>
public interface IGeocodingService
{
    Task<IReadOnlyList<GeocodedAddress>> SearchAsync(string query, string language, CancellationToken cancellationToken);
    Task<GeocodedAddress?> ReverseAsync(double latitude, double longitude, string language, CancellationToken cancellationToken);
}
