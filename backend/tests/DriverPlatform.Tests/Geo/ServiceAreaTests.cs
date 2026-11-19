using DriverPlatform.Application.Geo;

namespace DriverPlatform.Tests.Geo;

/// <summary>Each server sets where trips are served (Maps:ServiceArea): all of Europe by default, or a list.</summary>
public class ServiceAreaTests
{
    private static bool Served(string area, double latitude, double longitude) =>
        GeoMath.IsIn(GeoMath.ParseServiceArea(area).Boxes, new Coordinate(latitude, longitude));

    [Theory]
    [InlineData(41.9028, 12.4964)]   // Rome
    [InlineData(47.3769, 8.5417)]    // Zurich
    [InlineData(48.2082, 16.3738)]   // Vienna
    [InlineData(51.5072, -0.1276)]   // London
    [InlineData(40.4168, -3.7038)]   // Madrid
    [InlineData(38.7223, -9.1393)]   // Lisbon
    [InlineData(37.9838, 23.7275)]   // Athens
    [InlineData(35.3387, 25.1442)]   // Heraklion, Crete
    [InlineData(60.1699, 24.9384)]   // Helsinki
    [InlineData(69.6492, 18.9553)]   // Tromsø
    [InlineData(64.1466, -21.9426)]  // Reykjavik
    [InlineData(35.1856, 33.3823)]   // Nicosia
    [InlineData(28.1235, -15.4363)]  // Las Palmas, Canary Islands
    public void All_of_europe_is_served_by_default(double latitude, double longitude) =>
        Assert.True(Served("europe", latitude, longitude));

    [Theory]
    [InlineData(40.7128, -74.0060)]  // New York
    [InlineData(55.7558, 37.6173)]   // Moscow
    [InlineData(30.0444, 31.2357)]   // Cairo
    public void Places_outside_europe_are_not(double latitude, double longitude) =>
        Assert.False(Served("europe", latitude, longitude));

    [Theory]
    [InlineData(41.9028, 12.4964, true)]   // Rome
    [InlineData(41.9192, 8.7386, true)]    // Ajaccio, Corsica
    [InlineData(46.2044, 6.1432, true)]    // Geneva
    [InlineData(53.5511, 9.9937, true)]    // Hamburg
    [InlineData(48.2082, 16.3738, false)]  // Vienna
    [InlineData(51.5072, -0.1276, false)]  // London
    [InlineData(40.4168, -3.7038, false)]  // Madrid
    public void A_list_of_countries_serves_only_those(double latitude, double longitude, bool served) =>
        Assert.Equal(served, Served("it,fr,ch,de", latitude, longitude));

    [Fact]
    public void The_address_search_follows_the_setting()
    {
        Assert.Contains("gb", GeoMath.ParseServiceArea(null).Countries);
        Assert.Equal(["it", "fr"], GeoMath.ParseServiceArea(" IT, fr ").Countries);
        Assert.Throws<InvalidOperationException>(() => GeoMath.ParseServiceArea("it,us"));
    }
}
