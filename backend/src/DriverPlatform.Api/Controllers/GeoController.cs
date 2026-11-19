using System.ComponentModel.DataAnnotations;
using DriverPlatform.Application.Geo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DriverPlatform.Api.Controllers;

[ApiController]
[Route("api/geo")]
[EnableRateLimiting(RateLimitPolicies.Public)]
public sealed class GeoController(IPlaceSearchService places) : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<PlaceDto>>> Search(
        [FromQuery, Required, MinLength(3), MaxLength(200)] string q,
        [FromQuery, RegularExpression("^[a-z]{2}$")] string lang = "en",
        CancellationToken cancellationToken = default) =>
        Ok(await places.SearchAsync(q, lang, cancellationToken));

    [HttpGet("reverse")]
    public async Task<ActionResult<PlaceDto>> Reverse(
        [FromQuery, Range(-90, 90)] double lat,
        [FromQuery, Range(-180, 180)] double lng,
        [FromQuery, RegularExpression("^[a-z]{2}$")] string lang = "en",
        CancellationToken cancellationToken = default)
    {
        var place = await places.ReverseAsync(lat, lng, lang, cancellationToken);
        return place is null ? NotFound() : Ok(place);
    }

    /// <summary>The time zone at a point (IANA id, e.g. "Europe/London"): pickup times are in local time there.</summary>
    [HttpGet("time-zone")]
    [ResponseCache(Duration = 86400)]
    public ActionResult<TimeZoneDto> TimeZone(
        [FromQuery, Range(-90, 90)] double lat,
        [FromQuery, Range(-180, 180)] double lng) =>
        Ok(new TimeZoneDto(RideTime.ZoneAt(lat, lng).Id));
}

public sealed record TimeZoneDto(string TimeZone);
