using System.Net;
using System.Net.Http.Json;
using DriverPlatform.Application.BookingCom;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Dispatch;
using DriverPlatform.Application.Geo;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Infrastructure.Persistence;
using DriverPlatform.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DriverPlatform.Tests.BookingCom;

public sealed class BookingComSyncTests
{
    private const string Customer = "46825434";
    private const string Leg = "34745155";

    private static DateOnly Today() => DateOnly.FromDateTime(ItalyTime.ToLocal(DateTimeOffset.UtcNow));

    /// <summary>Venice airport to Rialto, as Booking.com sends it: the pickup in UTC, without an offset.</summary>
    private static BcBooking Ride(DateTime? pickupUtc = null, string status = BcStatus.New) => new()
    {
        CustomerReference = Customer,
        BookingReference = Leg,
        Status = status,
        PickupDateTime = pickupUtc ?? DateTime.UtcNow.AddHours(2.5).AddTicks(-(DateTime.UtcNow.Ticks % TimeSpan.TicksPerMinute)),
        PickupTimeZone = "Europe/Rome",
        FlightNumber = "fr6835",
        VehicleType = "EXECUTIVE",
        PassengerCount = 2,
        MeetAndGreet = true,
        AdditionalComments = "Two big suitcases",
        Ancillaries = [new BcAncillary { Type = "CHILD_SEAT", Quantity = 1 }],
        Price = new BcPrice { Amount = "84.50", Currency = "EUR" },
        Passenger = new BcPassenger { Title = "ms", Name = "Gillian Anne Tans", TelephoneNumber = "+44 7545 485868" },
        Pickup = new BcLocation { Latitude = 45.5050, Longitude = 12.3420, Address = "Via Galileo Galilei 30, Tessera VE", EstablishmentName = "Venice Marco Polo Airport (VCE)", Type = "AIRPORT" },
        Dropoff = new BcLocation { Latitude = 45.4380, Longitude = 12.3359, Address = "Ponte di Rialto, Venezia", Type = "STREET_ADDRESS" },
    };

    private static async Task<BookingComSyncResult> SyncAsync(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IBookingComSyncService>().SyncAsync(CancellationToken.None);
    }

    private static async Task<Domain.Entities.Booking> OurBookingAsync(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Bookings
            .Include(b => b.Client).Include(b => b.Partner)
            .SingleAsync(b => b.ExternalReference == Leg);
    }

    [Fact]
    public async Task A_new_ride_becomes_a_booking_with_booking_coms_price_and_is_accepted()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();
        factory.BookingCom.Put(Ride());

        var response = await admin.PostAsync("/api/admin/booking-com/sync", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<BookingComSyncResult>(TestData.Json))!;
        Assert.Equal((1, 1, 0), (result.Imported, result.Accepted, result.Errors));

        var booking = await OurBookingAsync(factory);
        Assert.Equal(BookingSource.Booking, booking.Source);
        Assert.Equal(BookingStatus.New, booking.Status);
        Assert.Equal(84.50m, booking.TotalPrice);
        Assert.Equal("EUR", booking.Currency);
        Assert.Equal(VehicleCategory.Business, booking.VehicleCategory);
        Assert.Equal("FR6835", booking.FlightNumber);
        Assert.True(booking.NameSign);
        Assert.Equal("Gillian Anne Tans", booking.NameSignText);
        Assert.Equal(("Gillian Anne", "Tans", "+447545485868"), (booking.Client.FirstName, booking.Client.LastName, booking.Client.Phone));
        Assert.Equal(ChildSeatGroup.Toddler, Assert.Single(booking.ChildSeats).Group);
        Assert.StartsWith("Venice Marco Polo Airport (VCE), ", booking.Pickup.Address);
        Assert.Equal(TimeSpan.Zero, booking.PickupAt.Offset);
        Assert.Equal(DateTime.SpecifyKind(factory.BookingCom[Leg].PickupDateTime, DateTimeKind.Utc), booking.PickupAt.UtcDateTime);
        Assert.True(booking.DistanceKm > 0);
        Assert.Equal(("Booking.com", PartnerKind.Booking, false), (booking.Partner!.Name, booking.Partner.Kind, booking.Partner.NotifyPassengers));
        Assert.Equal(BcStatus.Accepted, factory.BookingCom[Leg].Status);

        // The next round changes nothing.
        var again = await SyncAsync(factory);
        Assert.Equal((0, 0, 0), (again.Imported, again.Accepted, again.Amended));
    }

    [Fact]
    public async Task The_driver_is_sent_once_confirmed_and_pickup_and_drop_off_become_driver_events()
    {
        using var factory = new ApiFactory();
        var staff = await factory.CreateDispatcherClientAsync();
        var anonymous = factory.CreateClient();
        factory.BookingCom.Put(Ride());
        await SyncAsync(factory);
        var booking = await OurBookingAsync(factory);

        var driver = (await (await staff.PostAsJsonAsync("/api/admin/drivers", new DriverRequest
        {
            FirstName = "Marco",
            LastName = "Bianchi",
            Phone = "+39 340 000 0001",
            Documents =
            [
                new DriverDocumentDto { Type = DriverDocumentType.DrivingLicence, ExpiresOn = Today().AddYears(3) },
                new DriverDocumentDto { Type = DriverDocumentType.NccLicence, ExpiresOn = Today().AddYears(3) },
            ],
        }, TestData.Json)).Content.ReadFromJsonAsync<DriverDto>(TestData.Json))!;
        var vehicle = (await (await staff.PostAsJsonAsync("/api/admin/vehicles", new VehicleRequest
        {
            PlateNumber = "GA123BC",
            Make = "Mercedes-Benz",
            Model = "E-Class",
            Color = "Black",
            Category = VehicleCategory.Business,
            Seats = 3,
            LuggageCapacity = 3,
            ChildSeatsAvailable = 1,
            InsuranceExpiresOn = Today().AddYears(1),
            InspectionExpiresOn = Today().AddYears(1),
        }, TestData.Json)).Content.ReadFromJsonAsync<VehicleDto>(TestData.Json))!;
        var shift = (await (await staff.PostAsJsonAsync("/api/admin/shifts",
            new ShiftRequest { DriverId = driver.Id, VehicleId = vehicle.Id, BookingIds = [booking.Id] }, TestData.Json))
            .Content.ReadFromJsonAsync<ShiftResult>(TestData.Json))!.Shift;
        (await staff.PutAsJsonAsync($"/api/admin/shifts/{shift.Id}/status", new ShiftStatusRequest { Status = AssignmentStatus.Sent }, TestData.Json)).EnsureSuccessStatusCode();
        var token = (await staff.GetFromJsonAsync<DriverLinkResponse>($"/api/admin/drivers/{driver.Id}/link", TestData.Json))!.Url.Split('/').Last();

        // Sent but not confirmed: the driver may still say no.
        await SyncAsync(factory);
        Assert.Empty(factory.BookingCom.Drivers);

        (await anonymous.PostAsync($"/api/driver/{token}/shifts/{shift.Id}/confirm", null)).EnsureSuccessStatusCode();
        var sent = await SyncAsync(factory);
        Assert.Equal(1, sent.DriversSent);
        var assignment = Assert.Single(factory.BookingCom.Drivers).Assignment;
        Assert.Equal(("Marco", "Bianchi", "+393400000001"), (assignment.Driver.FirstName, assignment.Driver.LastName, assignment.Driver.TelephoneNumber));
        Assert.Equal(("Mercedes-Benz", "E-Class", "GA123BC", "Black"), (assignment.Vehicle.Make, assignment.Vehicle.Model, assignment.Vehicle.Registration, assignment.Vehicle.Colour));
        Assert.Equal(BcStatus.DriverAssigned, factory.BookingCom[Leg].Status);

        (await anonymous.PostAsync($"/api/driver/{token}/bookings/{booking.Id}/picked-up", null)).EnsureSuccessStatusCode();
        await SyncAsync(factory);
        (await anonymous.PostAsync($"/api/driver/{token}/bookings/{booking.Id}/completed", null)).EnsureSuccessStatusCode();
        await SyncAsync(factory);
        await SyncAsync(factory);

        Assert.Equal(
            [BcEvent.DepartedToPickup, BcEvent.ArrivedAtPickup, BcEvent.DepartedToDropoff, BcEvent.ArrivedAtDropoff],
            factory.BookingCom.Events.Select(e => e.Event));
        Assert.Single(factory.BookingCom.Drivers);
    }

    [Fact]
    public async Task Amendments_update_the_booking_and_a_customer_cancellation_cancels_it()
    {
        using var factory = new ApiFactory();
        factory.BookingCom.Put(Ride());
        await SyncAsync(factory);

        var later = DateTime.UtcNow.AddDays(3).Date.AddHours(9);
        factory.BookingCom.Put(Ride(later, BcStatus.PendingAmendment) with { PassengerCount = 3 });
        var amended = await SyncAsync(factory);
        var booking = await OurBookingAsync(factory);
        Assert.Equal((1, 1), (amended.Amended, amended.Accepted));
        Assert.Equal(later, booking.PickupAt.UtcDateTime);
        Assert.Equal(3, booking.Passengers);
        Assert.Equal(BcStatus.Accepted, factory.BookingCom[Leg].Status);

        factory.BookingCom.Put(factory.BookingCom[Leg] with { Status = BcStatus.PendingCancellation, Price = new BcPrice { Amount = "0" } });
        var cancelled = await SyncAsync(factory);
        booking = await OurBookingAsync(factory);
        Assert.Equal(1, cancelled.Cancelled);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(84.50m, booking.TotalPrice);
        // Acknowledged, never rejected back.
        Assert.Equal(BcStatus.Cancelled, factory.BookingCom[Leg].Status);
        Assert.DoesNotContain(factory.BookingCom.Responses, r => r.Response == "REJECT");
    }

    [Fact]
    public async Task Without_auto_accept_a_ride_waits_and_cancelling_it_here_rejects_it()
    {
        using var factory = new ApiFactory();
        var staff = await factory.CreateDispatcherClientAsync();
        factory.Services.GetRequiredService<IOptions<BookingComOptions>>().Value.AutoAccept = false;
        factory.BookingCom.Put(Ride());

        await SyncAsync(factory);
        Assert.Equal(BcStatus.New, factory.BookingCom[Leg].Status);

        var booking = await OurBookingAsync(factory);
        (await staff.PutAsJsonAsync($"/api/admin/bookings/{booking.Id}/status",
            new BookingStatusRequest { Status = BookingStatus.Cancelled, Reason = "No car free" }, TestData.Json)).EnsureSuccessStatusCode();
        var result = await SyncAsync(factory);

        Assert.Equal(1, result.Rejected);
        Assert.Equal(("REJECT", "OTHER"), (factory.BookingCom.Responses.Single().Response, factory.BookingCom.Responses.Single().Reason));
        Assert.Equal(BcStatus.Declined, factory.BookingCom[Leg].Status);
    }

    [Theory]
    [InlineData("STANDARD", VehicleCategory.Sedan)]
    [InlineData("ELECTRIC_STANDARD", VehicleCategory.Sedan)]
    [InlineData("EXECUTIVE", VehicleCategory.Business)]
    [InlineData("LUXURY", VehicleCategory.Estate)]
    [InlineData("PEOPLE_CARRIER", VehicleCategory.Van)]
    [InlineData("MINIBUS", VehicleCategory.Van)]
    [InlineData(null, VehicleCategory.Sedan)]
    public void Booking_com_car_types_map_on_our_tariffs(string? type, VehicleCategory expected) =>
        Assert.Equal(expected, BookingComSyncService.Category(type));
}
