using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.DriverPortal;
using DriverPlatform.Application.Pricing;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DriverPlatform.Infrastructure.Persistence;

/// <summary>Fills an empty database with the starting tariffs from the business plan and a first admin.</summary>
public sealed class DbSeeder(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    IDriverLinks driverLinks,
    IConfiguration configuration,
    ILogger<DbSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedAdminAsync(cancellationToken);
        await SeedPricingAsync(cancellationToken);
        await SeedHolidaysAsync(cancellationToken);
        await SeedSettingsAsync(cancellationToken);
        await SeedZonesAsync(cancellationToken);
        if (configuration.GetValue<bool>("Bootstrap:DemoFleet"))
        {
            await SeedDemoFleetAsync(cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
        await SeedTranslationsAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedAdminAsync(CancellationToken cancellationToken)
    {
        var email = configuration["Bootstrap:AdminEmail"]?.Trim().ToLowerInvariant();
        var password = configuration["Bootstrap:AdminPassword"];
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)
            || await db.StaffUsers.AnyAsync(cancellationToken))
        {
            return;
        }

        db.StaffUsers.Add(new StaffUser
        {
            Email = email,
            FullName = "Administrator",
            PasswordHash = passwordHasher.Hash(password),
            Role = StaffRole.Admin,
        });
        logger.LogInformation("Created bootstrap admin {Email}", email);
    }

    private async Task SeedPricingAsync(CancellationToken cancellationToken)
    {
        if (await db.VehicleRates.AnyAsync(cancellationToken))
        {
            return;
        }

        // Starting tariffs (business plan §4) — working assumptions, editable in the admin panel.
        db.VehicleRates.AddRange(
            new VehicleRate { Category = VehicleCategory.Sedan, BaseFee = 10m, PricePerKm = 1.60m, MaxPassengers = 3, MaxLuggage = 3 },
            new VehicleRate { Category = VehicleCategory.Estate, BaseFee = 12m, PricePerKm = 1.90m, MaxPassengers = 4, MaxLuggage = 5 },
            new VehicleRate { Category = VehicleCategory.Van, BaseFee = 15m, PricePerKm = 2.20m, MaxPassengers = 8, MaxLuggage = 8 });

        db.SurchargeRules.AddRange(
            new SurchargeRule
            {
                Name = "Weekend (Fri–Sun)",
                Kind = SurchargeKind.DaysOfWeek,
                Percent = 10m,
                DaysOfWeek = [DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday],
            },
            new SurchargeRule
            {
                Name = "Night 22:00–06:00",
                Kind = SurchargeKind.TimeOfDay,
                Percent = 20m,
                StartTime = new TimeOnly(22, 0),
                EndTime = new TimeOnly(6, 0),
            },
            new SurchargeRule
            {
                Name = "Peak season (July–August)",
                Kind = SurchargeKind.DateRange,
                Percent = 25m,
                StartDate = new DateOnly(2027, 7, 1),
                EndDate = new DateOnly(2027, 8, 31),
                RepeatsYearly = true,
            });

        db.ExtraFees.AddRange(
            new ExtraFee { Type = ExtraType.ChildSeat, Amount = 10m },
            new ExtraFee { Type = ExtraType.ExtraLuggage, Amount = 5m },
            new ExtraFee { Type = ExtraType.NameSign, Amount = 10m });
    }

    private const string HolidayPrefix = "Public holiday";

    /// <summary>Italian public holidays, +20% (business plan §4). Easter Monday moves, so it is entered per year.</summary>
    private async Task SeedHolidaysAsync(CancellationToken cancellationToken)
    {
        if (await db.SurchargeRules.AnyAsync(r => r.Name.StartsWith(HolidayPrefix), cancellationToken))
        {
            return;
        }

        SurchargeRule Holiday(string name, int month, int day, bool yearly = true, int year = 2027) => new()
        {
            Name = $"{HolidayPrefix} – {name}",
            Kind = SurchargeKind.DateRange,
            Percent = 20m,
            StartDate = new DateOnly(year, month, day),
            EndDate = new DateOnly(year, month, day),
            RepeatsYearly = yearly,
        };

        db.SurchargeRules.AddRange(
            Holiday("New Year's Day", 1, 1),
            Holiday("Epiphany", 1, 6),
            Holiday("Easter Monday 2027", 3, 29, yearly: false),
            Holiday("Liberation Day", 4, 25),
            Holiday("Labour Day", 5, 1),
            Holiday("Republic Day", 6, 2),
            Holiday("Ferragosto", 8, 15),
            Holiday("All Saints' Day", 11, 1),
            Holiday("Immaculate Conception", 12, 8),
            Holiday("Christmas Day", 12, 25),
            Holiday("St Stephen's Day", 12, 26));
    }

    private async Task SeedSettingsAsync(CancellationToken cancellationToken)
    {
        if (await db.PricingSettings.AnyAsync(cancellationToken))
        {
            return;
        }
        db.PricingSettings.Add(new PricingSettings
        {
            VatRatePercent = PricingDefaults.VatRatePercent,
            ReturnDiscountPercent = 0m,
            MinLeadTimeHours = PricingDefaults.MinLeadTimeHours,
        });
    }

    /// <summary>
    /// Starting zones for the launch cities (§2). Shapes and fees are approximate and meant to be
    /// refined on the map in the admin panel.
    /// </summary>
    private async Task SeedZonesAsync(CancellationToken cancellationToken)
    {
        if (await db.Zones.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Zones.Add(new Zone
        {
            Name = "Venice historic centre",
            Type = ZoneType.NoCarAccess,
            Shape = ZoneShape.Polygon,
            // Main islands and Giudecca; Piazzale Roma, Tronchetto and the cruise terminal stay outside.
            Polygon =
            [
                Point(45.4480, 12.3200), Point(45.4490, 12.3330), Point(45.4450, 12.3460), Point(45.4390, 12.3600),
                Point(45.4300, 12.3720), Point(45.4235, 12.3640), Point(45.4215, 12.3400), Point(45.4235, 12.3130),
                Point(45.4290, 12.3170), Point(45.4320, 12.3225), Point(45.4365, 12.3205), Point(45.4400, 12.3200),
            ],
            MeetingPoints =
            [
                new MeetingPoint
                {
                    Name = "Piazzale Roma",
                    Address = "Piazzale Roma, 30135 Venezia VE",
                    Latitude = 45.4381, Longitude = 12.3180,
                    ClientInstructions = "Cars cannot enter the historic centre. Walk or take a water bus (vaporetto) to Piazzale Roma; your driver waits by the taxi rank.",
                    DriverInstructions = "Wait at the Piazzale Roma drop-off area by the taxi rank. The client arrives on foot or by vaporetto.",
                },
                new MeetingPoint
                {
                    Name = "Tronchetto",
                    Address = "Isola Nuova del Tronchetto, 30135 Venezia VE",
                    Latitude = 45.4404, Longitude = 12.3073,
                    ClientInstructions = "Take the People Mover from Piazzale Roma or vaporetto line 2 to Tronchetto; your driver waits at the car park entrance.",
                    DriverInstructions = "Wait at the Tronchetto car park entrance, next to the People Mover stop.",
                },
                new MeetingPoint
                {
                    Name = "Venice cruise terminal (Marittima)",
                    Address = "Stazione Marittima, 30135 Venezia VE",
                    Latitude = 45.4344, Longitude = 12.3166,
                    ClientInstructions = "Your driver waits outside the terminal building, holding a sign if you asked for one.",
                    DriverInstructions = "Wait outside the passenger terminal exit.",
                },
                new MeetingPoint
                {
                    Name = "Venezia Mestre station",
                    Address = "Stazione di Venezia Mestre, Piazzale Favretti, 30171 Venezia VE",
                    Latitude = 45.4826, Longitude = 12.2316,
                    ClientInstructions = "Trains from Venezia Santa Lucia reach Mestre in about 10 minutes. Your driver waits at the main exit.",
                    DriverInstructions = "Wait at the main station exit on Piazzale Favretti.",
                },
            ],
        });

        db.Zones.AddRange(
            LimitedTraffic("Rome ZTL (historic centre)", 41.8990, 12.4770, 1700, 5m),
            LimitedTraffic("Milan Area C", 45.4641, 9.1896, 1400, 7.50m),
            LimitedTraffic("Florence ZTL", 43.7731, 11.2560, 1600, 5m),
            Airport("Rome Fiumicino Airport (FCO)", 41.7999, 12.2462, 2500,
                "Fiumicino Terminal 3 arrivals", "Aeroporto di Roma-Fiumicino, Terminal 3 Arrivi, 00054 Fiumicino RM", 41.7934, 12.2522),
            Airport("Venice Marco Polo Airport (VCE)", 45.5048, 12.3410, 1500,
                "Marco Polo arrivals hall", "Aeroporto di Venezia Marco Polo, Viale Galileo Galilei 30, 30173 Venezia VE", 45.5050, 12.3420),
            Airport("Milan Malpensa Airport (MXP)", 45.6300, 8.7230, 3500,
                "Malpensa Terminal 1 arrivals", "Aeroporto di Milano Malpensa, Terminal 1, 21010 Ferno VA", 45.6273, 8.7114),
            Airport("Florence Airport (FLR)", 43.8086, 11.2010, 1000,
                "Florence Airport arrivals", "Aeroporto di Firenze, Via del Termine 11, 50127 Firenze FI", 43.8093, 11.2027),
            Airport("Naples Airport (NAP)", 40.8846, 14.2906, 1200,
                "Naples Airport arrivals", "Aeroporto di Napoli, Viale F. Ruffo di Calabria, 80144 Napoli NA", 40.8849, 14.2900));
    }

    /// <summary>
    /// Italian, German and French for the texts seeded above, keyed by the English text. Filled in
    /// only where nothing is translated yet, so it also reaches older databases and never
    /// overwrites what the staff typed.
    /// </summary>
    private static readonly Dictionary<string, Translations> SeededTranslations = new()
    {
        ["Weekend (Fri–Sun)"] = new() { It = "Weekend (ven–dom)", De = "Wochenende (Fr–So)", Fr = "Week-end (ven–dim)" },
        ["Night 22:00–06:00"] = new() { It = "Notte 22:00–06:00", De = "Nacht 22:00–06:00", Fr = "Nuit 22h00–06h00" },
        ["Peak season (July–August)"] = new() { It = "Alta stagione (luglio–agosto)", De = "Hochsaison (Juli–August)", Fr = "Haute saison (juillet–août)" },
        ["Cars cannot enter the historic centre. Walk or take a water bus (vaporetto) to Piazzale Roma; your driver waits by the taxi rank."] = new() { It = "Le auto non possono entrare nel centro storico. Raggiunga Piazzale Roma a piedi o in vaporetto; l'autista La aspetta vicino alla fermata dei taxi.", De = "Autos dürfen nicht in die Altstadt. Gehen Sie zu Fuß oder nehmen Sie einen Wasserbus (Vaporetto) zum Piazzale Roma; Ihr Fahrer wartet am Taxistand.", Fr = "Les voitures ne peuvent pas entrer dans le centre historique. Rejoignez Piazzale Roma à pied ou en vaporetto ; votre chauffeur vous attend près de la station de taxis." },
        ["Take the People Mover from Piazzale Roma or vaporetto line 2 to Tronchetto; your driver waits at the car park entrance."] = new() { It = "Prenda il People Mover da Piazzale Roma o il vaporetto linea 2 fino al Tronchetto; l'autista La aspetta all'ingresso del parcheggio.", De = "Nehmen Sie den People Mover ab Piazzale Roma oder die Vaporetto-Linie 2 zum Tronchetto; Ihr Fahrer wartet an der Einfahrt des Parkhauses.", Fr = "Prenez le People Mover depuis Piazzale Roma ou le vaporetto ligne 2 jusqu'à Tronchetto ; votre chauffeur vous attend à l'entrée du parking." },
        ["Your driver waits outside the terminal building, holding a sign if you asked for one."] = new() { It = "L'autista La aspetta fuori dal terminal, con un cartello se lo ha richiesto.", De = "Ihr Fahrer wartet vor dem Terminalgebäude, mit einem Schild, falls Sie eines bestellt haben.", Fr = "Votre chauffeur vous attend devant le terminal, avec une pancarte si vous l'avez demandée." },
        ["Trains from Venezia Santa Lucia reach Mestre in about 10 minutes. Your driver waits at the main exit."] = new() { It = "I treni da Venezia Santa Lucia arrivano a Mestre in circa 10 minuti. L'autista La aspetta all'uscita principale.", De = "Züge ab Venezia Santa Lucia erreichen Mestre in etwa 10 Minuten. Ihr Fahrer wartet am Hauptausgang.", Fr = "Les trains depuis Venezia Santa Lucia rejoignent Mestre en 10 minutes environ. Votre chauffeur vous attend à la sortie principale." },
        ["After customs, your driver waits in the arrivals hall, holding a sign if you asked for one. Flight delays are tracked."] = new() { It = "Dopo la dogana, l'autista La aspetta nella sala arrivi, con un cartello se lo ha richiesto. I ritardi del volo sono monitorati.", De = "Nach dem Zoll wartet Ihr Fahrer in der Ankunftshalle, mit einem Schild, falls Sie eines bestellt haben. Flugverspätungen werden verfolgt.", Fr = "Après la douane, votre chauffeur vous attend dans le hall des arrivées, avec une pancarte si vous l'avez demandée. Les retards de vol sont suivis." },
        ["Public holiday – New Year's Day"] = new() { It = "Festivo – Capodanno", De = "Feiertag – Neujahr", Fr = "Jour férié – Jour de l'an" },
        ["Public holiday – Epiphany"] = new() { It = "Festivo – Epifania", De = "Feiertag – Heilige Drei Könige", Fr = "Jour férié – Épiphanie" },
        ["Public holiday – Easter Monday 2027"] = new() { It = "Festivo – Lunedì dell'Angelo 2027", De = "Feiertag – Ostermontag 2027", Fr = "Jour férié – Lundi de Pâques 2027" },
        ["Public holiday – Liberation Day"] = new() { It = "Festivo – Festa della Liberazione", De = "Feiertag – Tag der Befreiung", Fr = "Jour férié – Fête de la Libération" },
        ["Public holiday – Labour Day"] = new() { It = "Festivo – Festa dei Lavoratori", De = "Feiertag – Tag der Arbeit", Fr = "Jour férié – Fête du Travail" },
        ["Public holiday – Republic Day"] = new() { It = "Festivo – Festa della Repubblica", De = "Feiertag – Tag der Republik", Fr = "Jour férié – Fête de la République" },
        ["Public holiday – Ferragosto"] = new() { It = "Festivo – Ferragosto", De = "Feiertag – Ferragosto (Mariä Himmelfahrt)", Fr = "Jour férié – Ferragosto (Assomption)" },
        ["Public holiday – All Saints' Day"] = new() { It = "Festivo – Ognissanti", De = "Feiertag – Allerheiligen", Fr = "Jour férié – Toussaint" },
        ["Public holiday – Immaculate Conception"] = new() { It = "Festivo – Immacolata Concezione", De = "Feiertag – Mariä Empfängnis", Fr = "Jour férié – Immaculée Conception" },
        ["Public holiday – Christmas Day"] = new() { It = "Festivo – Natale", De = "Feiertag – Weihnachten", Fr = "Jour férié – Noël" },
        ["Public holiday – St Stephen's Day"] = new() { It = "Festivo – Santo Stefano", De = "Feiertag – Stephanstag", Fr = "Jour férié – Saint-Étienne" },
    };

    private async Task SeedTranslationsAsync(CancellationToken cancellationToken)
    {
        foreach (var rule in await db.SurchargeRules.ToListAsync(cancellationToken))
        {
            if (rule.NameTranslations is null && SeededTranslations.TryGetValue(rule.Name, out var name))
            {
                rule.NameTranslations = Copy(name);
            }
        }
        foreach (var zone in await db.Zones.ToListAsync(cancellationToken))
        {
            var points = zone.MeetingPoints.Select(Translated).ToList();
            if (points.Any(p => p is not null))
            {
                // A new list, so the JSON column is saved.
                zone.MeetingPoints = zone.MeetingPoints.Select((m, i) => points[i] ?? m).ToList();
            }
        }
    }

    private static MeetingPoint? Translated(MeetingPoint point) =>
        point.ClientInstructionsTranslations is null
        && point.ClientInstructions is { } text
        && SeededTranslations.TryGetValue(text, out var translations)
            ? new MeetingPoint
            {
                Name = point.Name,
                Address = point.Address,
                Latitude = point.Latitude,
                Longitude = point.Longitude,
                ClientInstructions = point.ClientInstructions,
                ClientInstructionsTranslations = Copy(translations),
                DriverInstructions = point.DriverInstructions,
            }
            : null;

    // Each owner needs its own instance: the airports share one text.
    private static Translations Copy(Translations t) => new() { It = t.It, De = t.De, Fr = t.Fr };

    private static PolygonPoint Point(double latitude, double longitude) => new() { Latitude = latitude, Longitude = longitude };

    private static Zone LimitedTraffic(string name, double latitude, double longitude, double radius, decimal fee) => new()
    {
        Name = name,
        Type = ZoneType.LimitedTraffic,
        Shape = ZoneShape.Circle,
        CenterLatitude = latitude,
        CenterLongitude = longitude,
        RadiusMeters = radius,
        AccessFee = fee,
    };

    private static Zone Airport(
        string name, double latitude, double longitude, double radius,
        string pointName, string address, double pointLatitude, double pointLongitude) => new()
    {
        Name = name,
        Type = ZoneType.Airport,
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
                ClientInstructions = "After customs, your driver waits in the arrivals hall, holding a sign if you asked for one. Flight delays are tracked.",
                DriverInstructions = "Wait in the arrivals hall. Check the flight status before leaving.",
            },
        ],
    };

    /// <summary>Development only (Bootstrap:DemoFleet): a few drivers and cars to try the dispatcher panel with.</summary>
    private async Task SeedDemoFleetAsync(CancellationToken cancellationToken)
    {
        if (await db.Drivers.AnyAsync(cancellationToken) || await db.Vehicles.AnyAsync(cancellationToken))
        {
            return;
        }
        var year = DateTime.UtcNow.Year;

        Driver Demo(string first, string last, string phone, string[] languages, DateOnly nccExpiry) => new()
        {
            FirstName = first,
            LastName = last,
            Phone = phone,
            Languages = [.. languages],
            Documents =
            [
                new DriverDocument { Type = DriverDocumentType.DrivingLicence, Number = "U1" + phone[^6..], ExpiresOn = new DateOnly(year + 5, 3, 1) },
                new DriverDocument { Type = DriverDocumentType.NccLicence, Number = "NCC-" + phone[^4..], ExpiresOn = nccExpiry },
                new DriverDocument { Type = DriverDocumentType.ProfessionalCertificate, Number = "KB" + phone[^5..], ExpiresOn = new DateOnly(year + 3, 6, 30) },
            ],
        };
        Driver[] drivers =
        [
            Demo("Marco", "Bianchi", "+393401110001", ["it", "en"], new DateOnly(year + 2, 12, 31)),
            Demo("Giulia", "Ferrari", "+393401110002", ["it", "en", "fr"], new DateOnly(year + 1, 9, 30)),
            // Expires soon, so the dashboard shows an alert.
            Demo("Andrei", "Popescu", "+393401110003", ["ro", "it", "en"], DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20))),
        ];
        foreach (var driver in drivers)
        {
            driverLinks.EnsureHash(driver);
        }
        db.Drivers.AddRange(drivers);

        Vehicle Car(string plate, string make, string model, VehicleCategory category, int seats, int luggage, int childSeats) => new()
        {
            PlateNumber = plate,
            Make = make,
            Model = model,
            Color = "Black",
            Category = category,
            Seats = seats,
            LuggageCapacity = luggage,
            ChildSeatsAvailable = childSeats,
            InsuranceExpiresOn = new DateOnly(year + 1, 4, 30),
            InspectionExpiresOn = new DateOnly(year + 1, 11, 30),
        };
        db.Vehicles.AddRange(
            Car("GA123BC", "Mercedes-Benz", "E-Class", VehicleCategory.Sedan, 3, 3, 1),
            Car("GB456DE", "Mercedes-Benz", "E-Class Estate", VehicleCategory.Estate, 4, 5, 2),
            Car("GC789FG", "Mercedes-Benz", "V-Class", VehicleCategory.Van, 7, 8, 3));
        logger.LogInformation("Seeded the demo fleet");
    }
}
