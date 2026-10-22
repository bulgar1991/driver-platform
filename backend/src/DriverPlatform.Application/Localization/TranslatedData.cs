using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.ValueObjects;

namespace DriverPlatform.Application.Localization;

/// <summary>Texts the staff type in the admin panel (in English, with translations), in the client's language.</summary>
public static class TranslatedData
{
    public static string DisplayName(this Zone zone, Texts texts) => zone.NameTranslations.Pick(zone.Name, texts.Language);

    public static string DisplayName(this MeetingPoint point, Texts texts) => point.NameTranslations.Pick(point.Name, texts.Language);

    /// <summary>The client instructions in that language (English if not translated); null if there are none.</summary>
    public static string? Instructions(this MeetingPoint point, Texts texts) =>
        string.IsNullOrWhiteSpace(point.ClientInstructions)
            ? null
            : point.ClientInstructionsTranslations.Pick(point.ClientInstructions, texts.Language);
}
