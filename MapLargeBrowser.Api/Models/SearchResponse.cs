namespace MapLargeBrowser.Api.Models;

public sealed record SearchResponse(
    IReadOnlyList<FileEntry> Entries,
    int FileCount,
    int DirectoryCount,
    long TotalSize,
    bool Truncated);
