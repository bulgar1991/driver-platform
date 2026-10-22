namespace DriverPlatform.Infrastructure.Maps;

/// <summary>The parts of an address as OpenStreetMap returns them (all optional).</summary>
public sealed record AddressParts(
    string? Name = null,
    string? Road = null,
    string? HouseNumber = null,
    string? Postcode = null,
    string? City = null,
    /// <summary>The closest named area: neighbourhood, quarter or village part.</summary>
    string? Suburb = null,
    string? Region = null);

/// <summary>
/// Short addresses for people and drivers instead of OpenStreetMap's full display name (shop,
/// street, district, municipality, region, postcode, country): "Via del Corso 12, 00186 Rome".
/// </summary>
public static class AddressFormatter
{
    /// <param name="withName">
    /// Keep the place's own name ("Hotel Danieli, Riva degli Schiavoni 4196, 30122 Venice"): yes for
    /// a place someone searched for, no for a point clicked on the map, where it is whatever shop
    /// happens to be there.
    /// </param>
    public static (string Label, string Address)? Format(AddressParts a, bool withName)
    {
        // Venice has no street names, only the district and a number: "San Marco 1".
        // A number on its own ("1") means nothing, so it is left out; so is a "road" that is only a
        // number (map data sometimes names platforms or service lanes that way).
        a = a with { Road = a.Road is not null && a.Road.Any(char.IsLetter) ? a.Road : null };
        var street = a.Road is not null
            ? Join(" ", a.Road, a.HouseNumber)
            : a.Suburb is not null && a.HouseNumber is not null ? Join(" ", a.Suburb, a.HouseNumber) : null;
        var name = withName && !string.IsNullOrWhiteSpace(a.Name) && !Same(a.Name, a.Road) && !Same(a.Name, a.City)
            ? a.Name
            : null;
        // No street (a square, a village, a whole city): the district or region says where it is.
        var area = street is null ? (Same(a.Suburb, a.City) ? null : a.Suburb) : null;
        var town = Join(" ", a.Postcode, a.City);

        var address = Join(", ", name, street ?? area, town);
        if (address is null || (street is null && name is null && area is null && a.City is not null))
        {
            // Only the city: "Rome, Lazio" rather than "00118 Rome".
            address = Join(", ", a.City, a.Region) ?? address;
        }
        if (address is null)
        {
            return null;
        }

        var label = Join(", ", name ?? street ?? area ?? a.City, name is null && street is null && area is null ? a.Region : a.City);
        return (label ?? address, address);
    }

    private static string? Join(string separator, params string?[] parts)
    {
        var present = parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()).ToArray();
        return present.Length == 0 ? null : string.Join(separator, present);
    }

    private static bool Same(string? a, string? b) =>
        a is not null && b is not null && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
