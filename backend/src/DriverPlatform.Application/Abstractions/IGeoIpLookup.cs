using System.Net;

namespace DriverPlatform.Application.Abstractions;

/// <summary>Where an IP address is: ISO country code and city, from an offline database.</summary>
public interface IGeoIpLookup
{
    (string? Country, string? City) Lookup(IPAddress address);
}
