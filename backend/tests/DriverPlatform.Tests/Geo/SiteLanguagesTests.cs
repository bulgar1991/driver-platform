using DriverPlatform.Application.Common;

namespace DriverPlatform.Tests.Geo;

public class SiteLanguagesTests
{
    [Theory]
    [InlineData("it", "https://magnus.example")]
    [InlineData("en", "https://magnus.example/en")]
    [InlineData("de", "https://magnus.example/de")]
    [InlineData("FR", "https://magnus.example/fr")]
    [InlineData("es", "https://magnus.example/en")]  // no Spanish site: English
    [InlineData(null, "https://magnus.example")]
    public void Client_links_open_the_site_in_the_clients_language(string? language, string expected)
    {
        Assert.Equal(expected, SiteLanguages.BaseUrl("https://magnus.example/", language));
    }
}
