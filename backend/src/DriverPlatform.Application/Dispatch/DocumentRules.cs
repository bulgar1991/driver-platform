using System.Globalization;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Dispatch;

/// <summary>Business plan §7: driver and vehicle documents with expiry dates and an alert before they expire.</summary>
public static class DocumentRules
{
    public const int AlertDays = 30;

    public static string Label(DriverDocumentType type) => type switch
    {
        DriverDocumentType.DrivingLicence => "Driving licence",
        DriverDocumentType.NccLicence => "NCC licence",
        DriverDocumentType.TaxiLicence => "Taxi licence",
        DriverDocumentType.ProfessionalCertificate => "Professional certificate (CAP/KB)",
        DriverDocumentType.Insurance => "Insurance",
        DriverDocumentType.CriminalRecord => "Criminal record",
        _ => type.ToString(),
    };

    public static IEnumerable<(string Document, DateOnly ExpiresOn)> Dated(Driver driver) =>
        driver.Documents.Where(d => d.ExpiresOn is not null).Select(d => (Label(d.Type), d.ExpiresOn!.Value));

    public static IEnumerable<(string Document, DateOnly ExpiresOn)> Dated(Vehicle vehicle)
    {
        if (vehicle.InsuranceExpiresOn is { } insurance)
        {
            yield return ("Insurance", insurance);
        }
        if (vehicle.InspectionExpiresOn is { } inspection)
        {
            yield return ("Inspection (revisione)", inspection);
        }
    }

    /// <summary>Documents that will be expired on <paramref name="onDate"/>: the driver or car cannot work that day.</summary>
    public static List<string> ExpiredOn(Driver driver, DateOnly onDate) =>
        Dated(driver).Where(d => d.ExpiresOn < onDate).Select(d => Expired(d.Document, d.ExpiresOn)).ToList();

    public static List<string> ExpiredOn(Vehicle vehicle, DateOnly onDate) =>
        Dated(vehicle).Where(d => d.ExpiresOn < onDate).Select(d => Expired(d.Document, d.ExpiresOn)).ToList();

    public static List<string> Missing(Driver driver)
    {
        var types = driver.Documents.Select(d => d.Type).ToHashSet();
        var missing = new List<string>();
        if (!types.Contains(DriverDocumentType.DrivingLicence))
        {
            missing.Add("No driving licence on file");
        }
        if (!types.Contains(DriverDocumentType.NccLicence) && !types.Contains(DriverDocumentType.TaxiLicence))
        {
            missing.Add("No NCC or taxi licence on file");
        }
        return missing;
    }

    public static List<string> Missing(Vehicle vehicle)
    {
        var missing = new List<string>();
        if (vehicle.InsuranceExpiresOn is null)
        {
            missing.Add("No insurance date on file");
        }
        if (vehicle.InspectionExpiresOn is null)
        {
            missing.Add("No inspection date on file");
        }
        return missing;
    }

    private static string Expired(string document, DateOnly date) =>
        $"{document} expired on {date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";

    public static int AlertCount(IEnumerable<(string Document, DateOnly ExpiresOn)> documents, DateOnly today) =>
        documents.Count(d => d.ExpiresOn <= today.AddDays(AlertDays));
}
