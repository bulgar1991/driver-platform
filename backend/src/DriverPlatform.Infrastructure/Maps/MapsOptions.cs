namespace DriverPlatform.Infrastructure.Maps;

public sealed class MapsOptions
{
    public const string SectionName = "Maps";

    /// <summary>OSRM server. The public demo server is for development only; production needs its own or a paid provider.</summary>
    public string OsrmBaseUrl { get; set; } = "https://router.project-osrm.org";
    /// <summary>Nominatim server. The public one allows at most one request per second.</summary>
    public string NominatimBaseUrl { get; set; } = "https://nominatim.openstreetmap.org";
    /// <summary>Nominatim's usage policy requires an identifying User-Agent with a contact.</summary>
    public string UserAgent { get; set; } = "DriverPlatform/1.0";
    public int TimeoutSeconds { get; set; } = 8;
    /// <summary>
    /// Where trips are served: "europe" (default) or ISO country codes, e.g. "it,fr,ch,de". Addresses are
    /// searched and accepted only there (<see cref="Application.Geo.GeoMath.Countries"/>).
    /// </summary>
    public string ServiceArea { get; set; } = "europe";
}
