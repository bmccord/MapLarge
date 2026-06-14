using System.Net;
using MapLargeBrowser.Api.Tests.Fixtures;

namespace MapLargeBrowser.Api.Tests.Endpoints;

public sealed class SizeTests(CustomRootFactory factory) : CustomRootTestBase(factory)
{
    [Fact]
    public async Task Computes_recursive_size_for_directory()
    {
        var response = await Client.GetAsync("/api/size?path=documents");

        response.EnsureSuccessStatusCode();
        var size = long.Parse(await response.Content.ReadAsStringAsync());

        // documents has intro.txt + spec.md + .private.txt (recursive includes hidden)
        var expected =
            new FileInfo(AbsolutePathOnDisk("documents/intro.txt")).Length +
            new FileInfo(AbsolutePathOnDisk("documents/spec.md")).Length +
            new FileInfo(AbsolutePathOnDisk("documents/.private.txt")).Length;
        Assert.Equal(expected, size);
    }

    [Fact]
    public async Task Returns_file_size_when_path_is_a_file()
    {
        var response = await Client.GetAsync("/api/size?path=README.txt");

        response.EnsureSuccessStatusCode();
        var size = long.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new FileInfo(AbsolutePathOnDisk("README.txt")).Length, size);
    }

    [Fact]
    public async Task Empty_directory_returns_zero()
    {
        var response = await Client.GetAsync("/api/size?path=empty-folder");
        response.EnsureSuccessStatusCode();
        var size = long.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0L, size);
    }

    [Fact]
    public async Task Missing_path_returns_404()
    {
        var response = await Client.GetAsync("/api/size?path=ghost");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Path_escape_returns_400()
    {
        var response = await Client.GetAsync("/api/size?path=../escape");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
