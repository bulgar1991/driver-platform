using System.Security.Cryptography;
using System.Text;
using DriverPlatform.Application.Common;
using DriverPlatform.Domain.Entities;
using Microsoft.Extensions.Options;

namespace DriverPlatform.Application.Bookings;

public interface IBookingLinks
{
    string ClientToken(Booking booking);
    bool IsValid(Booking booking, string token);
    /// <summary>The client's page for paying, checking and downloading the invoice.</summary>
    string ManageUrl(Booking booking);
}

/// <summary>
/// Client links are an HMAC of the booking id, so nothing secret is stored and the same link can be
/// put in every email. The reference in the URL finds the booking; the token proves the link is ours.
/// </summary>
public sealed class BookingLinks(IOptions<LinksOptions> links, IOptions<PaymentsOptions> payments) : IBookingLinks
{
    public string ClientToken(Booking booking)
    {
        var key = Encoding.UTF8.GetBytes(links.Value.SigningKey);
        var mac = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"client-booking:{booking.Id:N}"));
        return Convert.ToBase64String(mac).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public bool IsValid(Booking booking, string token) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(ClientToken(booking)),
            Encoding.UTF8.GetBytes(token));

    public string ManageUrl(Booking booking) =>
        $"{payments.Value.FrontendBaseUrl.TrimEnd('/')}/my-booking/{booking.Reference}/{ClientToken(booking)}";
}
