using System.Net;
using System.Net.Http.Json;
using MapLargeBrowser.Api.Tests.Fixtures;

namespace MapLargeBrowser.Api.Tests.Endpoints;

public sealed class CopyTests(CustomRootFactory factory) : CustomRootTestBase(factory)
{
    [Fact]
    public async Task Copies_file()
    {
        var response = await Copy("README.txt", "README-copy.txt");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(File.Exists(AbsolutePathOnDisk("README.txt")));         // source intact
        Assert.True(File.Exists(AbsolutePathOnDisk("README-copy.txt")));   // copy created
    }

    [Fact]
    public async Task Copies_directory_recursively()
    {
        var response = await Copy("documents", "documents-copy");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(Directory.Exists(AbsolutePathOnDisk("documents")));
        Assert.True(File.Exists(AbsolutePathOnDisk("documents-copy/intro.txt")));
        Assert.True(File.Exists(AbsolutePathOnDisk("documents-copy/spec.md")));
    }

    [Fact]
    public async Task Copies_deeply_nested_directory()
    {
        var response = await Copy("nested", "nested-copy");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(File.Exists(AbsolutePathOnDisk("nested-copy/level1/level2/deep.txt")));
    }

    [Fact]
    public async Task Overwrite_true_replaces_destination()
    {
        var response = await Copy("notes.md", "README.txt", overwrite: true);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var content = await File.ReadAllTextAsync(AbsolutePathOnDisk("README.txt"));
        Assert.Contains("Notes", content);
        // source still exists
        Assert.True(File.Exists(AbsolutePathOnDisk("notes.md")));
    }

    [Fact]
    public async Task Conflict_without_overwrite_returns_409()
    {
        var response = await Copy("notes.md", "README.txt");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Copying_root_returns_400()
    {
        var response = await Copy("", "copy-of-root");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Contains("browse root", problem.Detail);
    }

    [Fact]
    public async Task Copy_into_self_returns_400()
    {
        var response = await Copy("documents", "documents/sub");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Contains("into itself", problem.Detail);
    }

    [Fact]
    public async Task Missing_source_returns_404()
    {
        var response = await Copy("does-not-exist", "x");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Missing_destination_parent_returns_400()
    {
        var response = await Copy("README.txt", "ghost-dir/README.txt");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Source_path_escape_returns_400()
    {
        var response = await Copy("../escape", "x");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private Task<HttpResponseMessage> Copy(string from, string to, bool overwrite = false)
    {
        var url = "/api/entries/copy" + (overwrite ? "?overwrite=true" : "");
        return Client.PostAsJsonAsync(url, new EntryReferenceDto(from, to));
    }
}
