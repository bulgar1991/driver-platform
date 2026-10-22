using DriverPlatform.Domain.ValueObjects;

namespace DriverPlatform.Application.Localization;

/// <summary>Texts the staff type in the admin panel, in the client's language.</summary>
public static class TranslatedData
{
    /// <summary>The client instructions in that language (English if not translated); null if there are none.</summary>
    public static string? Instructions(this MeetingPoint point, Texts texts) =>
        string.IsNullOrWhiteSpace(point.ClientInstructions)
            ? null
            : point.ClientInstructionsTranslations.Pick(point.ClientInstructions, texts.Language);
}
