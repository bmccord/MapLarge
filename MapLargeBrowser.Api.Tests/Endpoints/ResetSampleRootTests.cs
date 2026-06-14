using System.Net;
using MapLargeBrowser.Api.Tests.Fixtures;

namespace MapLargeBrowser.Api.Tests.Endpoints;

/// <summary>
/// Tests reset against the fallback-root scenario where IsFallback = true.
/// SampleSeedingStartup runs at host startup and populates the SampleRoot directory.
/// </summary>
[Collection("FallbackRoot")]
public sealed class ResetSampleRootFallbackTests
{
    private readonly FallbackRootFactory _factory;
    private readonly HttpClient _client;

    public ResetSampleRootFallbackTests(FallbackRootFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Returns_204_and_repopulates_sample_root()
    {
        // First, mutate the seeded data so we can verify reset restored it.
        var marker = Path.Combine(_factory.SampleRoot, "marker.txt");
        await File.WriteAllTextAsync(marker, "marker");

        // Sanity: marker is visible via the API
        var browseBefore = await _client.GetAsync("/api/browse");
        var bodyBefore = await browseBefore.ReadJsonAsync<BrowseResponseDto>();
        Assert.Contains(bodyBefore.Entries, e => e.Name == "marker.txt");
        Assert.True(bodyBefore.RootIsResettable);

        // Reset
        var response = await _client.PostAsync("/api/admin/reset-sample-root", content: null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Marker gone, seeded content back
        Assert.False(File.Exists(marker));
        var browseAfter = await _client.GetAsync("/api/browse");
        var bodyAfter = await browseAfter.ReadJsonAsync<BrowseResponseDto>();
        Assert.DoesNotContain(bodyAfter.Entries, e => e.Name == "marker.txt");
        Assert.Contains(bodyAfter.Entries, e => e.Name == "README.txt");
    }

    [Fact]
    public async Task Seeding_runs_at_startup()
    {
        var response = await _client.GetAsync("/api/browse");
        response.EnsureSuccessStatusCode();
        var body = await response.ReadJsonAsync<BrowseResponseDto>();

        // SampleSeeder populates README.txt, notes.md, and several folders
        Assert.Contains(body.Entries, e => e.Name == "README.txt");
        Assert.Contains(body.Entries, e => e.Name == "documents" && e.Type == "Directory");
    }

    [Fact]
    public async Task Browse_response_marks_root_as_resettable()
    {
        var response = await _client.GetAsync("/api/browse");
        var body = await response.ReadJsonAsync<BrowseResponseDto>();
        Assert.True(body.RootIsResettable);
    }
}

/// <summary>
/// Tests reset against the custom-root scenario where IsFallback = false.
/// Reset endpoint should refuse with 403.
/// </summary>
public sealed class ResetSampleRootCustomTests(CustomRootFactory factory) : CustomRootTestBase(factory)
{
    [Fact]
    public async Task Returns_403_when_root_is_custom()
    {
        var response = await Client.PostAsync("/api/admin/reset-sample-root", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Contains("SampleRoot", problem.Detail);
    }

    [Fact]
    public async Task Browse_response_marks_root_as_not_resettable()
    {
        var response = await Client.GetAsync("/api/browse");
        var body = await response.ReadJsonAsync<BrowseResponseDto>();
        Assert.False(body.RootIsResettable);
    }
}
