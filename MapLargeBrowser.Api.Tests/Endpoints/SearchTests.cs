using System.Net;
using MapLargeBrowser.Api.Tests.Fixtures;

namespace MapLargeBrowser.Api.Tests.Endpoints;

public sealed class SearchTests(CustomRootFactory factory) : CustomRootTestBase(factory)
{
    [Fact]
    public async Task Substring_match_returns_matching_entries()
    {
        var response = await Client.GetAsync("/api/search?q=sample");

        response.EnsureSuccessStatusCode();
        var body = await response.ReadJsonAsync<SearchResponseDto>();

        Assert.Contains(body.Entries, e => e.RelativePath == "code/sample.ts");
        Assert.Equal(1, body.FileCount);
        Assert.False(body.Truncated);
    }

    [Fact]
    public async Task Walks_subdirectories_recursively()
    {
        var response = await Client.GetAsync("/api/search?q=deep");
        var body = await response.ReadJsonAsync<SearchResponseDto>();

        Assert.Contains(body.Entries, e => e.RelativePath == "nested/level1/level2/deep.txt");
    }

    [Fact]
    public async Task Search_is_case_insensitive()
    {
        var response = await Client.GetAsync("/api/search?q=SAMPLE");
        var body = await response.ReadJsonAsync<SearchResponseDto>();

        Assert.Contains(body.Entries, e => e.RelativePath == "code/sample.ts");
    }

    [Fact]
    public async Task Empty_query_returns_empty_results()
    {
        var response = await Client.GetAsync("/api/search?q=");

        response.EnsureSuccessStatusCode();
        var body = await response.ReadJsonAsync<SearchResponseDto>();
        Assert.Empty(body.Entries);
    }

    [Fact]
    public async Task ShowHidden_filter_works()
    {
        var withoutHidden = await Client.GetAsync("/api/search?q=.hidden");
        var hiddenBody = await withoutHidden.ReadJsonAsync<SearchResponseDto>();
        Assert.DoesNotContain(hiddenBody.Entries, e => e.Name == ".hidden-root.txt");

        var withHidden = await Client.GetAsync("/api/search?q=.hidden&showHidden=true");
        var visibleBody = await withHidden.ReadJsonAsync<SearchResponseDto>();
        Assert.Contains(visibleBody.Entries, e => e.Name == ".hidden-root.txt");
    }

    [Fact]
    public async Task Total_size_only_counts_matched_files()
    {
        var response = await Client.GetAsync("/api/search?q=intro.txt");
        var body = await response.ReadJsonAsync<SearchResponseDto>();

        Assert.Single(body.Entries);
        var expectedSize = new FileInfo(AbsolutePathOnDisk("documents/intro.txt")).Length;
        Assert.Equal(expectedSize, body.TotalSize);
    }

    [Fact]
    public async Task Missing_directory_returns_404()
    {
        var response = await Client.GetAsync("/api/search?path=does-not-exist&q=anything");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Path_escape_attempt_returns_400()
    {
        var response = await Client.GetAsync("/api/search?path=../escape&q=x");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
