using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Application.Localization;

namespace DriverPlatform.Application.Dispatch;

/// <summary>Business plan §7: driver and vehicle documents with expiry dates and an alert before they expire.</summary>
public static class DocumentRules
{
    public const int AlertDays = 30;

    /// <summary>Document names and messages are in the language of the request (the panel's).</summary>
    public static string Label(DriverDocumentType type) => Texts.Current.DocumentName(type);

    public static IEnumerable<(string Document, DateOnly ExpiresOn)> Dated(Driver driver) =>
        driver.Documents.Where(d => d.ExpiresOn is not null).Select(d => (Label(d.Type), d.ExpiresOn!.Value));

    public static IEnumerable<(string Document, DateOnly ExpiresOn)> Dated(Vehicle vehicle)
    {
        if (vehicle.InsuranceExpiresOn is { } insurance)
        {
            yield return (Texts.Current.VehicleInsurance, insurance);
        }
        if (vehicle.InspectionExpiresOn is { } inspection)
        {
            yield return (Texts.Current.VehicleInspection, inspection);
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
            missing.Add(Texts.Current.NoDrivingLicence);
        }
        if (!types.Contains(DriverDocumentType.NccLicence) && !types.Contains(DriverDocumentType.TaxiLicence))
        {
            missing.Add(Texts.Current.NoNccOrTaxiLicence);
        }
        return missing;
    }

    public static List<string> Missing(Vehicle vehicle)
    {
        var missing = new List<string>();
        if (vehicle.InsuranceExpiresOn is null)
        {
            missing.Add(Texts.Current.NoInsuranceDate);
        }
        if (vehicle.InspectionExpiresOn is null)
        {
            missing.Add(Texts.Current.NoInspectionDate);
        }
        return missing;
    }

    private static string Expired(string document, DateOnly date) => Texts.Current.DocumentExpired(document, date);

    public static int AlertCount(IEnumerable<(string Document, DateOnly ExpiresOn)> documents, DateOnly today) =>
        documents.Count(d => d.ExpiresOn <= today.AddDays(AlertDays));
}
