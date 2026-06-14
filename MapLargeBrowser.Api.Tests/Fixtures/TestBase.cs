using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MapLargeBrowser.Api.Tests.Fixtures;

/// <summary>
/// xUnit collection so all CustomRoot-using tests share one factory.
/// Setting MAPLARGE_BROWSER_ROOT is process-wide, so parallel factories
/// would race; the collection serializes them.
/// </summary>
[CollectionDefinition("CustomRoot")]
public sealed class CustomRootCollection : ICollectionFixture<CustomRootFactory> { }

/// <summary>
/// Same pattern for the FallbackRootFactory. Different collection so it
/// doesn't share serialization slot with custom-root tests.
/// </summary>
[CollectionDefinition("FallbackRoot")]
public sealed class FallbackRootCollection : ICollectionFixture<FallbackRootFactory> { }

/// <summary>
/// Base class for tests using CustomRootFactory. Resets the test directory
/// before each test (xUnit creates a fresh instance per test method).
/// </summary>
[Collection("CustomRoot")]
public abstract class CustomRootTestBase : IAsyncLifetime
{
    protected readonly CustomRootFactory Factory;
    protected readonly HttpClient Client;

    protected CustomRootTestBase(CustomRootFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    public Task InitializeAsync()
    {
        Factory.ResetTestRoot();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected string AbsolutePathOnDisk(string relativePath) =>
        Path.Combine(Factory.TestRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
}

/// <summary>
/// Simple DTO for parsing ProblemDetails responses without pulling in the full
/// Microsoft.AspNetCore.Mvc surface from the test project.
/// </summary>
public sealed record ProblemDetailsDto(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    string? Instance);

/// <summary>JSON helpers shared across tests.</summary>
public static class JsonHelpers
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<T> ReadJsonAsync<T>(this HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, Options)
            ?? throw new InvalidOperationException("Response body deserialized to null");
    }

    public static async Task<ProblemDetailsDto> ReadProblemAsync(this HttpResponseMessage response) =>
        await response.ReadJsonAsync<ProblemDetailsDto>();
}

/// <summary>
/// DTOs mirroring the API's response shape. Defined locally so the test
/// project doesn't have to depend on the API's Models for non-shared types.
/// </summary>
public sealed record FileEntryDto(
    string Name,
    string RelativePath,
    string Type,
    long Size,
    DateTime ModifiedUtc,
    string? SymlinkTarget);

public sealed record BrowseResponseDto(
    string Path,
    List<FileEntryDto> Entries,
    int FileCount,
    int DirectoryCount,
    long ImmediateSize,
    bool RootIsResettable);

public sealed record SearchResponseDto(
    List<FileEntryDto> Entries,
    int FileCount,
    int DirectoryCount,
    long TotalSize,
    bool Truncated);

public sealed record EntryReferenceDto(string From, string To);
