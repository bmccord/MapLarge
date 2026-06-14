using System.Net;
using MapLargeBrowser.Api.Tests.Fixtures;

namespace MapLargeBrowser.Api.Tests.Endpoints;

public sealed class DeleteTests(CustomRootFactory factory) : CustomRootTestBase(factory)
{
    [Fact]
    public async Task Deletes_file()
    {
        var response = await Client.DeleteAsync("/api/entries?path=README.txt");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(File.Exists(AbsolutePathOnDisk("README.txt")));
    }

    [Fact]
    public async Task Deletes_empty_directory()
    {
        var response = await Client.DeleteAsync("/api/entries?path=empty-folder");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(Directory.Exists(AbsolutePathOnDisk("empty-folder")));
    }

    [Fact]
    public async Task Non_empty_directory_returns_409_without_recursive()
    {
        var response = await Client.DeleteAsync("/api/entries?path=documents");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Contains("not empty", problem.Detail);
        Assert.True(Directory.Exists(AbsolutePathOnDisk("documents")));
    }

    [Fact]
    public async Task Recursive_true_deletes_directory_and_contents()
    {
        var response = await Client.DeleteAsync("/api/entries?path=documents&recursive=true");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(Directory.Exists(AbsolutePathOnDisk("documents")));
    }

    [Fact]
    public async Task Deletes_deeply_nested_directory_recursively()
    {
        var response = await Client.DeleteAsync("/api/entries?path=nested&recursive=true");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(Directory.Exists(AbsolutePathOnDisk("nested")));
    }

    [Fact]
    public async Task Deleting_root_returns_400()
    {
        var response = await Client.DeleteAsync("/api/entries?path=");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Contains("browse root", problem.Detail);
    }

    [Fact]
    public async Task Missing_path_returns_404()
    {
        var response = await Client.DeleteAsync("/api/entries?path=does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Path_escape_returns_400()
    {
        var response = await Client.DeleteAsync("/api/entries?path=../escape");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
