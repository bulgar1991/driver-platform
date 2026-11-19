using DriverPlatform.Application.Common;

namespace DriverPlatform.Tests.Geo;

public class SiteLanguagesTests
{
    [Theory]
    // English is the main language by default: at the root.
    [InlineData("en", "https://magnus.example")]
    [InlineData("it", "https://magnus.example/it")]
    [InlineData("de", "https://magnus.example/de")]
    [InlineData("FR", "https://magnus.example/fr")]
    [InlineData("es", "https://magnus.example")]  // no Spanish site: English
    [InlineData(null, "https://magnus.example")]
    public void Client_links_open_the_site_in_the_clients_language(string? language, string expected)
    {
        Assert.Equal(expected, SiteLanguages.BaseUrl("https://magnus.example/", language));
    }

    [Theory]
    [InlineData("en", "en", "")]
    [InlineData("it", "en", "/it")]
    [InlineData("de", "en", "/de")]
    [InlineData("es", "en", "")]   // unknown → English, the root when English is main
    [InlineData("es", "it", "/en")]
    [InlineData(null, "en", "")]
    public void The_main_language_is_at_the_root(string? language, string main, string expected) =>
        Assert.Equal(expected, SiteLanguages.PathPrefix(language, main));

    [Fact]
    public void Only_the_site_languages_can_be_main()
    {
        Assert.Equal("en", SiteLanguages.NormalizeMain(null));
        Assert.Equal("it", SiteLanguages.NormalizeMain(" IT "));
        Assert.Throws<InvalidOperationException>(() => SiteLanguages.NormalizeMain("es"));
    }
}
