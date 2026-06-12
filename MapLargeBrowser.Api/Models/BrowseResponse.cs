namespace MapLargeBrowser.Api.Models;

public sealed record BrowseResponse(
    string Path,
    IReadOnlyList<FileEntry> Entries,
    int FileCount,
    int DirectoryCount,
    long ImmediateSize,
    bool RootIsResettable);
