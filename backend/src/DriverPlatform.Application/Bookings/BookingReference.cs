using System.Security.Cryptography;

namespace DriverPlatform.Application.Bookings;

public static class BookingReference
{
    // No 0/O or 1/I/L, so it can be read out over the phone.
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    public static string Create() => "MD-" + RandomNumberGenerator.GetString(Alphabet, 6);
}
