using System.Net;
using MapLargeBrowser.Api.Tests.Fixtures;

namespace MapLargeBrowser.Api.Tests.Endpoints;

public sealed class DownloadTests(CustomRootFactory factory) : CustomRootTestBase(factory)
{
    [Fact]
    public async Task Returns_file_content_with_octet_stream()
    {
        var response = await Client.GetAsync("/api/download?path=README.txt");

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("Test root readme.", body);
    }

    [Fact]
    public async Task Sets_content_disposition_filename()
    {
        var response = await Client.GetAsync("/api/download?path=documents/spec.md");
        response.EnsureSuccessStatusCode();

        var disposition = response.Content.Headers.ContentDisposition;
        Assert.NotNull(disposition);
        Assert.Equal("spec.md", disposition!.FileName?.Trim('"'));
    }

    [Fact]
    public async Task Returns_404_for_missing_file()
    {
        var response = await Client.GetAsync("/api/download?path=does-not-exist.txt");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Returns_404_when_path_is_a_directory()
    {
        var response = await Client.GetAsync("/api/download?path=documents");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Path_escape_returns_400()
    {
        var response = await Client.GetAsync("/api/download?path=../escape.txt");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
