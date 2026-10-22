using System.Net;
using System.Net.Http.Json;
using DriverPlatform.Application.Bookings;
using DriverPlatform.Application.Localization;
using DriverPlatform.Application.Quotes;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;
using DriverPlatform.Infrastructure.Persistence;
using DriverPlatform.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DriverPlatform.Tests.Booking;

/// <summary>Price lines, errors and emails come back in the language of the site the client uses.</summary>
public sealed class LanguageTests
{
    private static HttpClient Client(ApiFactory factory, string language)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        return client;
    }

    [Fact]
    public async Task Price_lines_follow_the_language_of_the_request()
    {
        using var factory = new ApiFactory();
        var trip = TestData.Trip() with { NameSign = true };

        var german = await Client(factory, "de").QuoteAsync(trip);
        var english = await factory.CreateClient().QuoteAsync(trip);

        var lines = german.Vehicles.Single(v => v.Category == VehicleCategory.Sedan).Outbound.Lines;
        Assert.StartsWith("Limousine · ", lines[0].Label);
        Assert.EndsWith(" km", lines[0].Label);
        Assert.Contains(lines, l => l.Label == "Namensschild");
        Assert.Contains(english.Vehicles[0].Outbound.Lines, l => l.Label == "Name sign");
    }

    [Fact]
    public async Task Surcharge_names_use_the_translation_typed_in_the_panel()
    {
        using var factory = new ApiFactory();
        var trip = TestData.Trip() with { PickupAt = TestData.QuietPickup().Date.AddHours(23) };

        var italian = await Client(factory, "it").QuoteAsync(trip);
        var french = await Client(factory, "fr-CH, fr;q=0.9").QuoteAsync(trip);

        Assert.Equal("Notte 22:00–06:00", Assert.Single(italian.Outbound.Surcharges).Name);
        Assert.Contains(french.Vehicles[0].Outbound.Lines, l => l.Label == "Nuit 22h00–06h00 +20%");
    }

    [Fact]
    public async Task Zone_and_meeting_point_names_are_translated_but_the_english_name_stays_the_key()
    {
        using var factory = new ApiFactory();
        var florenceStation = new PointInput { Address = "Firenze SMN", Latitude = 43.7764, Longitude = 11.2480 };
        var trip = TestData.Trip(TestData.VeniceRialto, florenceStation) with { PickupMeetingPoint = "Venice cruise terminal (Marittima)" };

        var german = await Client(factory, "de").QuoteAsync(trip);

        Assert.Equal("Altstadt von Venedig", german.Outbound.PickupZone?.Name);
        var point = german.Outbound.PickupMeetingPoint!;
        Assert.Equal("Venice cruise terminal (Marittima)", point.Name);
        Assert.Equal("Kreuzfahrtterminal Venedig (Marittima)", point.NameTranslations?.De);
        Assert.Contains(german.Vehicles[0].Outbound.Lines, l => l.Label == "Zufahrt ZTL Florenz");
    }

    [Fact]
    public async Task Address_search_finds_meeting_points_by_their_translated_name()
    {
        using var factory = new ApiFactory();

        var places = await factory.CreateClient()
            .GetFromJsonAsync<List<Application.Geo.PlaceDto>>("/api/geo/search?q=Flughafen%20Neapel&lang=de", TestData.Json);

        Assert.Equal("Flughafen Neapel, Ankunft · Flughafen Neapel (NAP)", places![0].Label);
    }

    [Fact]
    public async Task Errors_are_translated_and_unknown_languages_get_english()
    {
        using var factory = new ApiFactory();
        var london = new PointInput { Address = "London", Latitude = 51.5072, Longitude = -0.1276 };

        async Task<string> ErrorAsync(string language)
        {
            var response = await Client(factory, language).PostAsJsonAsync("/api/quotes", TestData.Trip(london), TestData.Json);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestData.Json);
            return problem!.Errors["pickup"][0];
        }

        Assert.Equal(Texts.Italian.OutsideServiceArea, await ErrorAsync("it"));
        Assert.Equal(Texts.German.OutsideServiceArea, await ErrorAsync("de-CH"));
        Assert.Equal(Texts.English.OutsideServiceArea, await ErrorAsync("es"));
    }

    [Fact]
    public async Task Confirmation_email_and_invoice_are_in_the_clients_language()
    {
        using var factory = new ApiFactory();
        var client = Client(factory, "it");
        var request = TestData.Booking(TestData.Trip()) with
        {
            Contact = TestData.Booking(TestData.Trip()).Contact with { Language = "it" },
        };

        var response = await client.PostAsJsonAsync("/api/bookings", request, TestData.Json);
        var booking = (await response.Content.ReadFromJsonAsync<CreateBookingResponse>(TestData.Json))!;
        var session = booking.PaymentUrl!.Split('/').Last();
        (await client.PostAsJsonAsync($"/api/dev/payments/{session}/complete", new { method = "Card" }, TestData.Json)).EnsureSuccessStatusCode();

        var email = Assert.Single(factory.Emails.Sent);
        Assert.Equal($"Prenotazione confermata – {booking.Reference}", email.Subject);
        Assert.StartsWith("Gentile Maria,", email.TextBody);
        Assert.Contains("(ora italiana)", email.HtmlBody);
        Assert.Matches(@"\d+,\d\d €", email.TextBody);
        Assert.Contains("Cordiali saluti,", email.TextBody);

        using var scope = factory.Services.CreateScope();
        var invoice = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Invoices.SingleAsync();
        Assert.Contains("(Berlina · ", invoice.Lines[0].Description);
    }

    [Fact]
    public void Every_language_formats_money_its_own_way()
    {
        Assert.Equal("€58.00", Texts.English.Money(58m));
        Assert.Equal("58,00 €", Texts.Italian.Money(58m));
        Assert.Equal("1.234,50 €", Texts.German.Money(1234.5m));
        // French groups thousands with a narrow no-break space.
        Assert.Matches(@"^1\s234,50\s€$", Texts.French.Money(1234.5m));
        Assert.Same(Texts.English, Texts.For("ru"));
    }

    [Fact]
    public void A_blank_translation_shows_the_english_text()
    {
        var translations = new Translations { It = "Notte", De = " " };

        Assert.Equal("Notte", translations.Pick("Night", "it"));
        Assert.Equal("Night", translations.Pick("Night", "de"));
        Assert.Equal("Night", ((Translations?)null).Pick("Night", "fr"));
        Assert.Null(new Translations { Fr = "  " }.Cleaned());
    }
}
