using DriverPlatform.Application.Bookings;

namespace DriverPlatform.Tests.Booking;

/// <summary>Each server sets its brand's letters; anything but 2–4 letters stops the start.</summary>
public sealed class BrandSettingsTests
{
    [Theory]
    [InlineData("hv", "HV-")]
    [InlineData(" HVT ", "HVT-")]
    public void Booking_references_start_with_the_brand_prefix(string prefix, string expected) =>
        Assert.Matches($"^{expected}[2-9A-Z]{{6}}$", BookingReference.Create(BookingReference.NormalizePrefix(prefix)));

    [Theory]
    [InlineData("H")]
    [InlineData("HVIPT")]
    [InlineData("H-V")]
    public void A_wrong_prefix_is_refused(string prefix) =>
        Assert.Throws<InvalidOperationException>(() => BookingReference.NormalizePrefix(prefix));
}
