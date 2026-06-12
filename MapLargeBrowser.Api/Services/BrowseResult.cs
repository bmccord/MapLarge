using MapLargeBrowser.Api.Models;

namespace MapLargeBrowser.Api.Services;

public sealed record BrowseResult(
    IReadOnlyList<FileEntry> Entries,
    int FileCount,
    int DirectoryCount,
    long ImmediateSize);
