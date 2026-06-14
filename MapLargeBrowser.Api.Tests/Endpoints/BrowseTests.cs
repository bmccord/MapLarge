using System.Net;
using MapLargeBrowser.Api.Tests.Fixtures;

namespace MapLargeBrowser.Api.Tests.Endpoints;

public sealed class BrowseTests(CustomRootFactory factory) : CustomRootTestBase(factory)
{
    [Fact]
    public async Task Returns_root_listing_with_counts_and_immediate_size()
    {
        var response = await Client.GetAsync("/api/browse");

        response.EnsureSuccessStatusCode();
        var body = await response.ReadJsonAsync<BrowseResponseDto>();

        Assert.Equal("", body.Path);
        // README.txt + notes.md + 4 directories (documents, code, empty-folder, nested); hidden not shown
        Assert.Equal(2, body.FileCount);
        Assert.Equal(4, body.DirectoryCount);
        Assert.Equal(6, body.Entries.Count);
        Assert.True(body.ImmediateSize > 0);
        Assert.False(body.RootIsResettable);
    }

    [Fact]
    public async Task Excludes_hidden_entries_by_default()
    {
        var response = await Client.GetAsync("/api/browse");
        var body = await response.ReadJsonAsync<BrowseResponseDto>();

        Assert.DoesNotContain(body.Entries, e => e.Name == ".hidden-root.txt");
    }

    [Fact]
    public async Task Includes_hidden_entries_when_showHidden_true()
    {
        var response = await Client.GetAsync("/api/browse?showHidden=true");
        var body = await response.ReadJsonAsync<BrowseResponseDto>();

        Assert.Contains(body.Entries, e => e.Name == ".hidden-root.txt");
    }

    [Fact]
    public async Task Sorts_directories_first_then_files_alphabetical()
    {
        var response = await Client.GetAsync("/api/browse");
        var body = await response.ReadJsonAsync<BrowseResponseDto>();

        var types = body.Entries.Select(e => e.Type).ToList();
        var firstFileIndex = types.IndexOf("File");
        var lastDirIndex = types.LastIndexOf("Directory");
        Assert.True(lastDirIndex < firstFileIndex, "Directories should come before files");
    }

    [Fact]
    public async Task Subdir_listing_works()
    {
        var response = await Client.GetAsync("/api/browse?path=documents");
        response.EnsureSuccessStatusCode();
        var body = await response.ReadJsonAsync<BrowseResponseDto>();

        Assert.Equal("documents", body.Path);
        Assert.Equal(2, body.FileCount); // intro.txt, spec.md (hidden excluded)
        Assert.Equal(0, body.DirectoryCount);
    }

    [Fact]
    public async Task Empty_folder_returns_zero_counts()
    {
        var response = await Client.GetAsync("/api/browse?path=empty-folder");
        var body = await response.ReadJsonAsync<BrowseResponseDto>();

        Assert.Empty(body.Entries);
        Assert.Equal(0, body.FileCount);
        Assert.Equal(0, body.DirectoryCount);
        Assert.Equal(0, body.ImmediateSize);
    }

    [Fact]
    public async Task Missing_directory_returns_404()
    {
        var response = await Client.GetAsync("/api/browse?path=does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Contains("Directory not found", problem.Detail);
    }

    [Fact]
    public async Task Path_escape_attempt_returns_400()
    {
        var response = await Client.GetAsync("/api/browse?path=../escape");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Contains("escape", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }
}
