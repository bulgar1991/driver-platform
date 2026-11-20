using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DriverPlatform.Application.Content;
using DriverPlatform.Tests.Infrastructure;

namespace DriverPlatform.Tests.Content;

/// <summary>Admin → Website content: texts and pictures of the public site.</summary>
public sealed class SiteContentTests
{
    private static MultipartFormDataContent File(byte[] data)
    {
        var file = new ByteArrayContent(data);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return new MultipartFormDataContent { { file, "file", "picture.png" } };
    }

    private static async Task<SiteContentDto> Public(HttpClient client) =>
        (await client.GetFromJsonAsync<SiteContentDto>("/api/content", TestData.Json))!;

    [Fact]
    public async Task An_admin_changes_a_text_the_site_shows_it_and_emptying_it_brings_the_default_back()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();
        using var anonymous = factory.CreateClient();
        Assert.Empty((await Public(anonymous)).Texts);

        var saved = await admin.PutAsJsonAsync("/api/admin/content/texts/welcome.title",
            new ContentText("Arrive in Zurich like a local", "Arrivi a Zurigo", "  ", null), TestData.Json);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var text = (await Public(anonymous)).Texts["welcome.title"];
        Assert.Equal("Arrive in Zurich like a local", text.En);
        Assert.Equal("Arrivi a Zurigo", text.It);
        // Blank is "use the site's own text".
        Assert.Null(text.De);

        await admin.PutAsJsonAsync("/api/admin/content/texts/welcome.title", new ContentText(null, null, null, null), TestData.Json);
        Assert.Empty((await Public(anonymous)).Texts);
    }

    [Fact]
    public async Task An_uploaded_picture_is_served_as_webp_with_its_size()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();
        using var anonymous = factory.CreateClient();

        var response = await admin.PutAsync("/api/admin/content/images/welcome.image", File(TestImages.Png(1254, 470)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var image = (await Public(anonymous)).Images["welcome.image"];
        Assert.Equal((1254, 470), (image.Width, image.Height));
        var served = await anonymous.GetAsync(image.Url);
        Assert.Equal("image/webp", served.Content.Headers.ContentType?.MediaType);

        await admin.DeleteAsync("/api/admin/content/images/welcome.image");
        Assert.Empty((await Public(anonymous)).Images);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(image.Url)).StatusCode);
    }

    [Fact]
    public async Task A_dispatcher_cannot_change_the_website()
    {
        using var factory = new ApiFactory();
        var dispatcher = await factory.CreateDispatcherClientAsync();

        var response = await dispatcher.PutAsJsonAsync("/api/admin/content/texts/welcome.title", new ContentText("x", null, null, null), TestData.Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Keys_text_lengths_and_picture_sizes_are_checked()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();

        var badKey = await admin.PutAsJsonAsync("/api/admin/content/texts/Welcome%20Title", new ContentText("x", null, null, null), TestData.Json);
        Assert.Equal(HttpStatusCode.BadRequest, badKey.StatusCode);

        var tooLong = await admin.PutAsJsonAsync("/api/admin/content/texts/welcome.title", new ContentText(new string('a', 2001), null, null, null), TestData.Json);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);

        var tooBig = await admin.PutAsync("/api/admin/content/images/welcome.image", File(TestImages.NoisyPng(600, 500)));
        Assert.Equal(HttpStatusCode.BadRequest, tooBig.StatusCode);
    }
}
