namespace DriverPlatform.Application.Common;

/// <summary>
/// Languages of the public site, one build each (frontend angular.json). The server's main language
/// (Site:Language, from SITE_LANGUAGE in deploy/.env; English by default) is at the root, the others
/// under /it, /en, /de, /fr. Client links open in the client's language.
/// </summary>
public static class SiteLanguages
{
    public static readonly IReadOnlyList<string> Codes = ["it", "en", "de", "fr"];

    /// <summary>The language at the root of the site; set once at startup.</summary>
    public static string Main { get; private set; } = "en";

    public static void UseMainLanguage(string? language) => Main = NormalizeMain(language);

    /// <summary>"IT " → "it"; empty → "en"; anything but it, en, de, fr is refused.</summary>
    public static string NormalizeMain(string? language)
    {
        var value = string.IsNullOrWhiteSpace(language) ? "en" : language.Trim().ToLowerInvariant();
        return Codes.Contains(value)
            ? value
            : throw new InvalidOperationException("Site:Language must be one of it, en, de, fr.");
    }

    /// <summary>
    /// With English as main: "de" → "/de", English → "", any other language (Spanish, Russian…) → English.
    /// The main language's prefix is always "".
    /// </summary>
    public static string PathPrefix(string? language) => PathPrefix(language, Main);

    public static string PathPrefix(string? language, string main)
    {
        if (language is null)
        {
            return "";
        }
        var code = language.Trim().ToLowerInvariant();
        if (!Codes.Contains(code))
        {
            code = "en";
        }
        return code == main ? "" : "/" + code;
    }

    /// <summary>Base URL of the site in that language, without a trailing slash.</summary>
    public static string BaseUrl(string frontendBaseUrl, string? language) =>
        frontendBaseUrl.TrimEnd('/') + PathPrefix(language);
}
