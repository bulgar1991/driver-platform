using System.Security.Cryptography;
using System.Text;
using DriverPlatform.Application.Common;
using DriverPlatform.Domain.Entities;
using Microsoft.Extensions.Options;

namespace DriverPlatform.Application.DriverPortal;

public interface IDriverLinks
{
    /// <summary>The driver's personal page with all their shifts (business plan §5).</summary>
    string Url(Driver driver);
    /// <summary>Makes sure the lookup hash matches the current link.</summary>
    void EnsureHash(Driver driver);
    /// <summary>Invalidates the old link (lost phone, driver leaves) and returns the new one.</summary>
    string Rotate(Driver driver);
    string Hash(string token);
}

/// <summary>
/// The token is an HMAC of the driver id and a version, so the same link can be put in every message
/// without storing it; only its SHA-256 is stored, to find the driver from the link.
/// </summary>
public sealed class DriverLinks(IOptions<LinksOptions> links, IOptions<PaymentsOptions> site) : IDriverLinks
{
    public string Url(Driver driver)
    {
        EnsureHash(driver);
        return $"{site.Value.FrontendBaseUrl.TrimEnd('/')}/driver/{Token(driver)}";
    }

    public void EnsureHash(Driver driver) => driver.AccessTokenHash = Hash(Token(driver));

    public string Rotate(Driver driver)
    {
        driver.LinkVersion++;
        return Url(driver);
    }

    public string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private string Token(Driver driver)
    {
        var key = Encoding.UTF8.GetBytes(links.Value.SigningKey);
        var mac = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"driver:{driver.Id:N}:{driver.LinkVersion}"));
        return Convert.ToBase64String(mac).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
