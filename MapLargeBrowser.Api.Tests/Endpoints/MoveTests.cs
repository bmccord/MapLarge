using System.Net;
using System.Net.Http.Json;
using MapLargeBrowser.Api.Tests.Fixtures;

namespace MapLargeBrowser.Api.Tests.Endpoints;

public sealed class MoveTests(CustomRootFactory factory) : CustomRootTestBase(factory)
{
    [Fact]
    public async Task Moves_file_within_root()
    {
        var response = await Move("README.txt", "renamed.txt");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(File.Exists(AbsolutePathOnDisk("README.txt")));
        Assert.True(File.Exists(AbsolutePathOnDisk("renamed.txt")));
    }

    [Fact]
    public async Task Moves_file_into_subdirectory()
    {
        var response = await Move("README.txt", "documents/README.txt");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(File.Exists(AbsolutePathOnDisk("documents/README.txt")));
    }

    [Fact]
    public async Task Moves_directory()
    {
        var response = await Move("documents", "renamed-documents");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(Directory.Exists(AbsolutePathOnDisk("documents")));
        Assert.True(Directory.Exists(AbsolutePathOnDisk("renamed-documents/intro.txt")) ||
                    File.Exists(AbsolutePathOnDisk("renamed-documents/intro.txt")));
    }

    [Fact]
    public async Task Overwrite_true_replaces_destination_file()
    {
        var response = await Move("notes.md", "README.txt", overwrite: true);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        // notes.md content is now at README.txt
        var newReadme = await File.ReadAllTextAsync(AbsolutePathOnDisk("README.txt"));
        Assert.Contains("Notes", newReadme);
    }

    [Fact]
    public async Task Conflict_without_overwrite_returns_409()
    {
        var response = await Move("notes.md", "README.txt");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Contains("already exists", problem.Detail);
    }

    [Fact]
    public async Task Moving_root_returns_400()
    {
        var response = await Move("", "elsewhere");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Contains("browse root", problem.Detail);
    }

    [Fact]
    public async Task Move_into_self_returns_400()
    {
        var response = await Move("documents", "documents/sub");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Contains("into itself", problem.Detail);
    }

    [Fact]
    public async Task Missing_source_returns_404()
    {
        var response = await Move("does-not-exist", "x");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Missing_destination_parent_returns_400()
    {
        var response = await Move("README.txt", "ghost-dir/README.txt");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Contains("parent", problem.Detail);
    }

    [Fact]
    public async Task Source_path_escape_returns_400()
    {
        var response = await Move("../escape", "x");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Destination_path_escape_returns_400()
    {
        var response = await Move("README.txt", "../escape.txt");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private Task<HttpResponseMessage> Move(string from, string to, bool overwrite = false)
    {
        var url = "/api/entries/move" + (overwrite ? "?overwrite=true" : "");
        return Client.PostAsJsonAsync(url, new EntryReferenceDto(from, to));
    }
}
