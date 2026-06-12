using MapLargeBrowser.Api.Models;

namespace MapLargeBrowser.Api.Services;

public sealed record SearchResult(
    IReadOnlyList<FileEntry> Entries,
    int FileCount,
    int DirectoryCount,
    long TotalSize,
    bool Truncated);
