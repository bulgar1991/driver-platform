using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DriverPlatform.Infrastructure.Persistence;

/// <summary>
/// Starting data for a server in Switzerland (Site:Region = "ch"): Swiss VAT, Swiss public holidays,
/// the main airports, stations and car-free resorts. On a database that already has the Italian
/// starting data, the Swiss items are added once and the Italian ones switched off (not deleted),
/// so the staff can still switch any of them back on.
/// </summary>
public sealed partial class DbSeeder
{
    /// <summary>Swiss standard VAT rate (2024–), also for passenger transport.</summary>
    public const decimal SwissVatRatePercent = 8.1m;

    private const decimal ItalianVatRatePercent = 10m;

    private static readonly string[] ItalianOnlyHolidays =
    [
        "Epiphany", "Liberation Day", "Labour Day", "Republic Day", "Ferragosto", "All Saints' Day", "Immaculate Conception",
    ];

    private static readonly string[] ItalianZones =
    [
        "Venice historic centre", "Rome ZTL (historic centre)", "Milan Area C", "Florence ZTL", "Rome Fiumicino Airport (FCO)",
        "Venice Marco Polo Airport (VCE)", "Milan Malpensa Airport (MXP)", "Florence Airport (FLR)", "Naples Airport (NAP)",
    ];

    private async Task SeedSwitzerlandAsync(CancellationToken cancellationToken)
    {
        await SeedSwissHolidaysAsync(cancellationToken);
        await SeedSwissZonesAsync(cancellationToken);
    }

    /// <summary>
    /// Holidays kept in (nearly) every canton, +20% like the Italian ones. Good Friday, Easter Monday,
    /// Ascension and Whit Monday move, so they are entered per year. Cantonal ones (Berchtold's Day,
    /// Labour Day in Zurich, Jeûne genevois…) can be added in the panel.
    /// </summary>
    private async Task SeedSwissHolidaysAsync(CancellationToken cancellationToken)
    {
        if (await db.SurchargeRules.AnyAsync(r => r.Name == $"{HolidayPrefix} – Swiss National Day", cancellationToken))
        {
            return;
        }

        var existing = await db.SurchargeRules.Where(r => r.Name.StartsWith(HolidayPrefix)).ToListAsync(cancellationToken);
        foreach (var rule in existing.Where(r => ItalianOnlyHolidays.Any(h => r.Name == $"{HolidayPrefix} – {h}")))
        {
            rule.IsActive = false;
        }
        var names = existing.Select(r => r.Name).ToHashSet();
        SurchargeRule[] swiss =
        [
            Holiday("New Year's Day", 1, 1),
            Holiday("Good Friday 2027", 3, 26, yearly: false),
            Holiday("Easter Monday 2027", 3, 29, yearly: false),
            Holiday("Ascension Day 2027", 5, 6, yearly: false),
            Holiday("Whit Monday 2027", 5, 17, yearly: false),
            Holiday("Swiss National Day", 8, 1),
            Holiday("Christmas Day", 12, 25),
            Holiday("St Stephen's Day", 12, 26),
        ];
        // Shared with Italy (New Year, Easter Monday, Christmas…): already there, kept as they are.
        db.SurchargeRules.AddRange(swiss.Where(r => !names.Contains(r.Name)));
        logger.LogInformation("Switched the public holidays to Switzerland");
    }

    private async Task SeedSwissZonesAsync(CancellationToken cancellationToken)
    {
        var swiss = SwissZones();
        var swissNames = swiss.Select(z => z.Name).ToList();
        if (await db.Zones.AnyAsync(z => swissNames.Contains(z.Name), cancellationToken))
        {
            return;
        }

        foreach (var zone in await db.Zones.Where(z => ItalianZones.Contains(z.Name)).ToListAsync(cancellationToken))
        {
            zone.IsActive = false;
        }
        db.Zones.AddRange(swiss);

        // The Italian rate, still as it was seeded: the Swiss one instead. A rate the staff set is kept.
        var settings = await db.PricingSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is not null && settings.VatRatePercent == ItalianVatRatePercent)
        {
            settings.VatRatePercent = SwissVatRatePercent;
        }
        logger.LogInformation("Switched the zones and VAT to Switzerland");
    }

    /// <summary>Shapes and points are approximate, to be refined on the map in the panel.</summary>
    private static List<Zone> SwissZones() =>
    [
        Airport("Zurich Airport (ZRH)", 47.4582, 8.5555, 2500,
            "Zurich Airport arrivals", "Flughafen Zürich, Ankunft 1 und 2, Airport Center, 8058 Zürich-Flughafen", 47.4503, 8.5624),
        Airport("Geneva Airport (GVA)", 46.2381, 6.1090, 2000,
            "Geneva Airport arrivals", "Aéroport de Genève, Route de l'Aéroport 21, 1215 Le Grand-Saconnex", 46.2306, 6.1093),
        Airport("EuroAirport Basel-Mulhouse (BSL)", 47.5896, 7.5299, 2000,
            "EuroAirport arrivals (Swiss side)", "EuroAirport Basel Mulhouse Freiburg, Swiss sector, 4030 Basel", 47.5996, 7.5291),
        Station("Zürich HB (main station)", 47.3779, 8.5403,
            "Zürich HB, Bahnhofplatz", "Bahnhofplatz, 8001 Zürich", 47.3772, 8.5400),
        Station("Geneva Cornavin station", 46.2102, 6.1424,
            "Place de Cornavin", "Place de Cornavin, 1201 Genève", 46.2093, 6.1426),
        Station("Bern station", 46.9490, 7.4391,
            "Bahnhofplatz Bern", "Bahnhofplatz, 3011 Bern", 46.9480, 7.4398),
        Station("Basel SBB station", 47.5476, 7.5897,
            "Centralbahnplatz Basel", "Centralbahnplatz, 4051 Basel", 47.5480, 7.5893),
        Station("Lucerne station", 47.0502, 8.3102,
            "Bahnhofplatz Luzern", "Bahnhofplatz, 6003 Luzern", 47.0505, 8.3093),
        CarFree("Zermatt (car-free)", 46.0207, 7.7491, 2500,
            "Täsch, Matterhorn Terminal", "Matterhorn Terminal Täsch, Bahnhofstrasse, 3929 Täsch", 46.0693, 7.7772,
            "Cars cannot drive into Zermatt. Take the shuttle train from Zermatt to Täsch (12 minutes, every 20 minutes); your driver waits at the Matterhorn Terminal.",
            "Wait at the Matterhorn Terminal drop-off in Täsch. The client arrives by shuttle train from Zermatt."),
        CarFree("Saas-Fee (car-free)", 46.1085, 7.9285, 1200,
            "Saas-Fee car park", "Parkhaus Saas-Fee, 3906 Saas-Fee", 46.1110, 7.9320,
            "Cars stop at the entrance of the village. Your driver waits at the multi-storey car park by the bus terminal.",
            "Wait at the multi-storey car park at the village entrance, by the bus terminal."),
    ];

    private static Zone Station(
        string name, double latitude, double longitude,
        string pointName, string address, double pointLatitude, double pointLongitude) => new()
    {
        Name = name,
        Type = ZoneType.TrainStation,
        Shape = ZoneShape.Circle,
        CenterLatitude = latitude,
        CenterLongitude = longitude,
        // Only the station itself: addresses around it keep their own pickup.
        RadiusMeters = 250,
        MeetingPoints =
        [
            new MeetingPoint
            {
                Name = pointName,
                Address = address,
                Latitude = pointLatitude,
                Longitude = pointLongitude,
                ClientInstructions = "Your driver waits at the main station entrance, holding a sign if you asked for one.",
                DriverInstructions = "Wait at the main entrance. Check the train's arrival before leaving.",
            },
        ],
    };

    private static Zone CarFree(
        string name, double latitude, double longitude, double radius,
        string pointName, string address, double pointLatitude, double pointLongitude,
        string clientInstructions, string driverInstructions) => new()
    {
        Name = name,
        Type = ZoneType.NoCarAccess,
        Shape = ZoneShape.Circle,
        CenterLatitude = latitude,
        CenterLongitude = longitude,
        RadiusMeters = radius,
        MeetingPoints =
        [
            new MeetingPoint
            {
                Name = pointName,
                Address = address,
                Latitude = pointLatitude,
                Longitude = pointLongitude,
                ClientInstructions = clientInstructions,
                DriverInstructions = driverInstructions,
            },
        ],
    };

    private static readonly Dictionary<string, Translations> SwissTranslations = new()
    {
        ["Zurich Airport (ZRH)"] = new() { It = "Aeroporto di Zurigo (ZRH)", De = "Flughafen Zürich (ZRH)", Fr = "Aéroport de Zurich (ZRH)" },
        ["Geneva Airport (GVA)"] = new() { It = "Aeroporto di Ginevra (GVA)", De = "Flughafen Genf (GVA)", Fr = "Aéroport de Genève (GVA)" },
        ["EuroAirport Basel-Mulhouse (BSL)"] = new() { It = "EuroAirport Basilea-Mulhouse (BSL)", De = "EuroAirport Basel-Mülhausen (BSL)", Fr = "EuroAirport Bâle-Mulhouse (BSL)" },
        ["Zürich HB (main station)"] = new() { It = "Zurigo HB (stazione centrale)", De = "Zürich HB (Hauptbahnhof)", Fr = "Zurich HB (gare centrale)" },
        ["Geneva Cornavin station"] = new() { It = "Stazione di Ginevra Cornavin", De = "Bahnhof Genf Cornavin", Fr = "Gare de Genève-Cornavin" },
        ["Bern station"] = new() { It = "Stazione di Berna", De = "Bahnhof Bern", Fr = "Gare de Berne" },
        ["Basel SBB station"] = new() { It = "Stazione di Basilea FFS", De = "Bahnhof Basel SBB", Fr = "Gare de Bâle CFF" },
        ["Lucerne station"] = new() { It = "Stazione di Lucerna", De = "Bahnhof Luzern", Fr = "Gare de Lucerne" },
        ["Zermatt (car-free)"] = new() { It = "Zermatt (senza auto)", De = "Zermatt (autofrei)", Fr = "Zermatt (sans voitures)" },
        ["Saas-Fee (car-free)"] = new() { It = "Saas-Fee (senza auto)", De = "Saas-Fee (autofrei)", Fr = "Saas-Fee (sans voitures)" },
        ["Zurich Airport arrivals"] = new() { It = "Aeroporto di Zurigo, arrivi", De = "Flughafen Zürich, Ankunft", Fr = "Aéroport de Zurich, arrivées" },
        ["Geneva Airport arrivals"] = new() { It = "Aeroporto di Ginevra, arrivi", De = "Flughafen Genf, Ankunft", Fr = "Aéroport de Genève, arrivées" },
        ["EuroAirport arrivals (Swiss side)"] = new() { It = "EuroAirport, arrivi (settore svizzero)", De = "EuroAirport, Ankunft (Schweizer Seite)", Fr = "EuroAirport, arrivées (secteur suisse)" },
        ["Zürich HB, Bahnhofplatz"] = new() { It = "Zurigo HB, Bahnhofplatz", De = "Zürich HB, Bahnhofplatz", Fr = "Zurich HB, Bahnhofplatz" },
        ["Bahnhofplatz Luzern"] = new() { It = "Bahnhofplatz, Lucerna", De = "Bahnhofplatz Luzern", Fr = "Bahnhofplatz, Lucerne" },
        ["Täsch, Matterhorn Terminal"] = new() { It = "Täsch, Matterhorn Terminal", De = "Täsch, Matterhorn Terminal", Fr = "Täsch, Matterhorn Terminal" },
        ["Saas-Fee car park"] = new() { It = "Parcheggio di Saas-Fee", De = "Parkhaus Saas-Fee", Fr = "Parking de Saas-Fee" },
        ["Your driver waits at the main station entrance, holding a sign if you asked for one."] = new() { It = "L'autista La aspetta all'ingresso principale della stazione, con un cartello se lo ha richiesto.", De = "Ihr Fahrer wartet am Haupteingang des Bahnhofs, mit einem Schild, falls Sie eines bestellt haben.", Fr = "Votre chauffeur vous attend à l'entrée principale de la gare, avec une pancarte si vous l'avez demandée." },
        ["Cars cannot drive into Zermatt. Take the shuttle train from Zermatt to Täsch (12 minutes, every 20 minutes); your driver waits at the Matterhorn Terminal."] = new() { It = "Le auto non possono entrare a Zermatt. Prenda il treno navetta da Zermatt a Täsch (12 minuti, ogni 20 minuti); l'autista La aspetta al Matterhorn Terminal.", De = "Autos dürfen nicht nach Zermatt fahren. Nehmen Sie den Shuttlezug von Zermatt nach Täsch (12 Minuten, alle 20 Minuten); Ihr Fahrer wartet am Matterhorn Terminal.", Fr = "Les voitures ne peuvent pas entrer à Zermatt. Prenez la navette ferroviaire de Zermatt à Täsch (12 minutes, toutes les 20 minutes) ; votre chauffeur vous attend au Matterhorn Terminal." },
        ["Cars stop at the entrance of the village. Your driver waits at the multi-storey car park by the bus terminal."] = new() { It = "Le auto si fermano all'ingresso del paese. L'autista La aspetta al parcheggio multipiano vicino al terminal dei bus.", De = "Autos halten am Dorfeingang. Ihr Fahrer wartet im Parkhaus beim Busterminal.", Fr = "Les voitures s'arrêtent à l'entrée du village. Votre chauffeur vous attend au parking couvert près de la gare routière." },
        ["Public holiday – Good Friday 2027"] = new() { It = "Festivo – Venerdì Santo 2027", De = "Feiertag – Karfreitag 2027", Fr = "Jour férié – Vendredi saint 2027" },
        ["Public holiday – Ascension Day 2027"] = new() { It = "Festivo – Ascensione 2027", De = "Feiertag – Auffahrt 2027", Fr = "Jour férié – Ascension 2027" },
        ["Public holiday – Whit Monday 2027"] = new() { It = "Festivo – Lunedì di Pentecoste 2027", De = "Feiertag – Pfingstmontag 2027", Fr = "Jour férié – Lundi de Pentecôte 2027" },
        ["Public holiday – Swiss National Day"] = new() { It = "Festivo – Festa nazionale svizzera", De = "Feiertag – Bundesfeiertag", Fr = "Jour férié – Fête nationale suisse" },
    };
}
