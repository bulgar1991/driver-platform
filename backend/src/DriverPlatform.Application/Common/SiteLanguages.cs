namespace DriverPlatform.Application.Common;

/// <summary>
/// Languages of the public site, one build each (frontend angular.json): Italian at the root,
/// English, German and French under /en, /de, /fr. Client links open in the client's language.
/// </summary>
public static class SiteLanguages
{
    private static readonly Dictionary<string, string> Prefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["it"] = "",
        ["en"] = "/en",
        ["de"] = "/de",
        ["fr"] = "/fr",
    };

    /// <summary>"de" → "/de"; Italian → ""; any other language (Spanish, Russian…) → English.</summary>
    public static string PathPrefix(string? language) =>
        language is null ? "" : Prefixes.GetValueOrDefault(language.Trim(), "/en");

    /// <summary>Base URL of the site in that language, without a trailing slash.</summary>
    public static string BaseUrl(string frontendBaseUrl, string? language) =>
        frontendBaseUrl.TrimEnd('/') + PathPrefix(language);
}
