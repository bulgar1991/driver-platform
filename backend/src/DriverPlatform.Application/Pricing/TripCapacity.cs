namespace DriverPlatform.Application.Pricing;

public static class TripCapacity
{
    /// <summary>Two small bags take the room of one large bag.</summary>
    public static int LuggageUnits(int largeLuggage, int smallLuggage) =>
        largeLuggage + (int)Math.Ceiling(smallLuggage / 2.0);
}
