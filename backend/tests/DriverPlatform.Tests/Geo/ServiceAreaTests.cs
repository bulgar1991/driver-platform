using DriverPlatform.Application.Geo;

namespace DriverPlatform.Tests.Geo;

public class ServiceAreaTests
{
    [Theory]
    [InlineData(41.9028, 12.4964)]  // Rome
    [InlineData(45.4642, 9.1900)]   // Milan
    [InlineData(37.5079, 15.0830)]  // Catania
    [InlineData(48.8566, 2.3522)]   // Paris
    [InlineData(43.7102, 7.2620)]   // Nice
    [InlineData(41.9192, 8.7386)]   // Ajaccio, Corsica
    [InlineData(47.3769, 8.5417)]   // Zurich
    [InlineData(46.2044, 6.1432)]   // Geneva
    [InlineData(48.1351, 11.5820)]  // Munich
    [InlineData(52.5200, 13.4050)]  // Berlin
    [InlineData(53.5511, 9.9937)]   // Hamburg
    public void Places_in_the_four_countries_are_served(double latitude, double longitude)
    {
        Assert.True(GeoMath.IsInServiceArea(new Coordinate(latitude, longitude)));
    }

    [Theory]
    [InlineData(48.2082, 16.3738)]  // Vienna
    [InlineData(51.5072, -0.1276)]  // London
    [InlineData(40.4168, -3.7038)]  // Madrid
    [InlineData(50.0755, 14.4378 + 1.0)]  // east of Prague
    public void Places_elsewhere_are_not(double latitude, double longitude)
    {
        Assert.False(GeoMath.IsInServiceArea(new Coordinate(latitude, longitude)));
    }
}
