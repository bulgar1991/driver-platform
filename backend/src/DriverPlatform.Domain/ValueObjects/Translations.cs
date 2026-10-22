using System.ComponentModel.DataAnnotations;

namespace DriverPlatform.Domain.ValueObjects;

/// <summary>
/// A text the staff type in English (a surcharge name, meeting point instructions), in the other
/// languages of the site. A blank translation falls back to the English text.
/// </summary>
public sealed class Translations
{
    [MaxLength(1000)]
    public string? It { get; set; }
    [MaxLength(1000)]
    public string? De { get; set; }
    [MaxLength(1000)]
    public string? Fr { get; set; }
}

public static class TranslationsExtensions
{
    /// <summary>The text in that language ("it", "de", "fr"), else <paramref name="english"/>. Rows saved before translations existed have none.</summary>
    public static string Pick(this Translations? translations, string english, string language)
    {
        var translated = language switch
        {
            "it" => translations?.It,
            "de" => translations?.De,
            "fr" => translations?.Fr,
            _ => null,
        };
        return string.IsNullOrWhiteSpace(translated) ? english : translated;
    }

    /// <summary>Trimmed, blanks removed; null when nothing is translated.</summary>
    public static Translations? Cleaned(this Translations? translations)
    {
        static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        var cleaned = new Translations { It = Clean(translations?.It), De = Clean(translations?.De), Fr = Clean(translations?.Fr) };
        return cleaned is { It: null, De: null, Fr: null } ? null : cleaned;
    }
}
