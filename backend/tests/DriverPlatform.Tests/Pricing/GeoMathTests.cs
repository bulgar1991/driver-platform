using DriverPlatform.Application.Geo;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Tests.Pricing;

public sealed class GeoMathTests
{
    [Fact]
    public void Distance_between_rome_and_milan_is_about_480_km()
    {
        var km = GeoMath.DistanceMeters(new Coordinate(41.9028, 12.4964), new Coordinate(45.4642, 9.1900)) / 1000;

        Assert.InRange(km, 470, 490);
    }

    [Fact]
    public void Circle_zone_contains_points_within_its_radius()
    {
        var zone = new Zone { Shape = ZoneShape.Circle, CenterLatitude = 43.7731, CenterLongitude = 11.2560, RadiusMeters = 1000 };

        Assert.True(GeoMath.Contains(zone, new Coordinate(43.7700, 11.2550)));
        Assert.False(GeoMath.Contains(zone, new Coordinate(43.7900, 11.2560)));
    }

    [Fact]
    public void Times_are_converted_from_italian_local_time()
    {
        // CET in winter (+1), CEST in summer (+2).
        Assert.Equal(new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.Zero), ItalyTime.ToUtc(new DateTime(2026, 12, 1, 10, 0, 0)));
        Assert.Equal(new DateTimeOffset(2027, 7, 1, 8, 0, 0, TimeSpan.Zero), ItalyTime.ToUtc(new DateTime(2027, 7, 1, 10, 0, 0)));
        // 28 March 2027, 02:30 is skipped when clocks go forward.
        Assert.Null(ItalyTime.ToUtc(new DateTime(2027, 3, 28, 2, 30, 0)));
    }
}
