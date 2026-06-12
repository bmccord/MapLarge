namespace MapLargeBrowser.Api.Services;

public interface IFileBrowser
{
    BrowseResult Browse(string absolutePath, bool showHidden);

    SearchResult Search(
        string absolutePath,
        string query,
        bool showHidden,
        CancellationToken cancellationToken);

    long GetSize(string absolutePath, CancellationToken cancellationToken);

    Stream OpenForDownload(string absolutePath);

    Task SaveUploadAsync(
        string targetDirectory,
        string fileName,
        Stream content,
        bool overwrite,
        CancellationToken cancellationToken);

    void DeleteEntry(string absolutePath, bool recursive);

    void Transfer(
        string fromAbsolute,
        string toAbsolute,
        bool overwrite,
        TransferKind kind);
}
