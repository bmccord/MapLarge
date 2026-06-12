namespace MapLargeBrowser.Api.Models;

public sealed record FileEntry(
    string Name,
    string RelativePath,
    EntryType Type,
    long Size,
    DateTime ModifiedUtc,
    string? SymlinkTarget);
