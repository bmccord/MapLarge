using MapLargeBrowser.Api.Models;

namespace MapLargeBrowser.Api.Services;

public sealed class FileBrowser(IPathResolver paths) : IFileBrowser
{
    private const int SearchCap = 500;

    public BrowseResult Browse(string absolutePath, bool showHidden)
    {
        var entries = new List<FileEntry>();
        var fileCount = 0;
        var directoryCount = 0;
        var immediateSize = 0L;

        foreach (var child in new DirectoryInfo(absolutePath).EnumerateFileSystemInfos())
        {
            if (!showHidden && IsHidden(child))
                continue;

            var entry = ToFileEntry(child);
            entries.Add(entry);

            switch (entry.Type)
            {
                case EntryType.File:
                    fileCount++;
                    immediateSize += entry.Size;
                    break;
                case EntryType.Directory:
                    directoryCount++;
                    break;
            }
        }

        entries.Sort(CompareEntries);

        return new BrowseResult(entries, fileCount, directoryCount, immediateSize);
    }

    public SearchResult Search(
        string absolutePath,
        string query,
        bool showHidden,
        CancellationToken cancellationToken)
    {
        var results = new List<FileEntry>(SearchCap);
        var fileCount = 0;
        var directoryCount = 0;
        var totalSize = 0L;
        var truncated = false;

        var stack = new Stack<string>();
        stack.Push(absolutePath);

        while (stack.Count > 0 && !truncated)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = stack.Pop();

            foreach (var child in SafeEnumerate(current))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!showHidden && IsHidden(child))
                    continue;

                var entry = ToFileEntry(child);

                if (entry.RelativePath.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(entry);

                    switch (entry.Type)
                    {
                        case EntryType.File:
                            fileCount++;
                            totalSize += entry.Size;
                            break;
                        case EntryType.Directory:
                            directoryCount++;
                            break;
                    }

                    if (results.Count >= SearchCap)
                    {
                        truncated = true;
                        break;
                    }
                }

                if (entry.Type != EntryType.Symlink && child is DirectoryInfo subDir)
                    stack.Push(subDir.FullName);
            }
        }

        results.Sort(static (a, b) =>
            string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase));

        return new SearchResult(results, fileCount, directoryCount, totalSize, truncated);
    }

    public long GetSize(string absolutePath, CancellationToken cancellationToken)
    {
        if (Directory.Exists(absolutePath))
            return ComputeDirectorySize(absolutePath, cancellationToken);

        if (File.Exists(absolutePath))
            return new FileInfo(absolutePath).Length;

        throw new FileNotFoundException($"Path not found: {absolutePath}");
    }

    public Stream OpenForDownload(string absolutePath)
    {
        if (!File.Exists(absolutePath))
            throw new FileNotFoundException($"File not found: {absolutePath}");

        return File.OpenRead(absolutePath);
    }

    public async Task SaveUploadAsync(
        string targetDirectory,
        string fileName,
        Stream content,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var target = Path.Combine(targetDirectory, fileName);

        if ((File.Exists(target) || Directory.Exists(target)) && !overwrite)
            throw new FileBrowserConflictException($"Destination already exists: {fileName}");

        await using var stream = File.Create(target);
        await content.CopyToAsync(stream, cancellationToken);
    }

    // No CancellationToken: mid-operation cancellation would leave the filesystem
    // in an inconsistent partial-mutation state (some descendants deleted, others not).
    // Treating these mutations as all-or-nothing is more correct than half-rolled-back.
    public void DeleteEntry(string absolutePath, bool recursive)
    {
        if (paths.IsRoot(absolutePath))
            throw new InvalidFileBrowserOperationException("Cannot delete the browse root.");

        if (Directory.Exists(absolutePath))
        {
            var hasChildren = Directory.EnumerateFileSystemEntries(absolutePath).Any();
            if (hasChildren && !recursive)
                throw new FileBrowserConflictException("Directory is not empty.");

            Directory.Delete(absolutePath, recursive);
            return;
        }

        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
            return;
        }

        throw new FileNotFoundException($"Path not found: {absolutePath}");
    }

    // No CancellationToken: see DeleteEntry. Same all-or-nothing rationale.
    public void Transfer(
        string fromAbsolute,
        string toAbsolute,
        bool overwrite,
        TransferKind kind)
    {
        if (paths.IsRoot(fromAbsolute))
            throw new InvalidFileBrowserOperationException("Cannot move or copy the browse root.");

        if (!Path.Exists(fromAbsolute))
            throw new FileNotFoundException($"Source not found: {fromAbsolute}");

        if (Path.Exists(toAbsolute) && !overwrite)
            throw new FileBrowserConflictException(
                $"Destination already exists: {paths.ToRelative(toAbsolute)}");

        var destParent = Path.GetDirectoryName(toAbsolute);
        if (string.IsNullOrEmpty(destParent) || !Directory.Exists(destParent))
            throw new InvalidFileBrowserOperationException("Destination parent directory does not exist.");

        if (Directory.Exists(fromAbsolute) && IsSelfOrDescendant(fromAbsolute, toAbsolute))
            throw new InvalidFileBrowserOperationException("Cannot move or copy a directory into itself.");

        switch (kind)
        {
            case TransferKind.Move:
                DoMove(fromAbsolute, toAbsolute);
                break;
            case TransferKind.Copy:
                DoCopy(fromAbsolute, toAbsolute, overwrite);
                break;
        }
    }

    private FileEntry ToFileEntry(FileSystemInfo info)
    {
        var isSymlink = (info.Attributes & FileAttributes.ReparsePoint) != 0;
        var type = isSymlink ? EntryType.Symlink
            : info is DirectoryInfo ? EntryType.Directory : EntryType.File;
        var size = !isSymlink && info is FileInfo file ? file.Length : 0L;
        var linkTarget = isSymlink ? info.LinkTarget : null;
        return new FileEntry(
            info.Name,
            paths.ToRelative(info.FullName),
            type,
            size,
            info.LastWriteTimeUtc,
            linkTarget);
    }

    private static IEnumerable<FileSystemInfo> SafeEnumerate(string directory)
    {
        try
        {
            return new DirectoryInfo(directory).EnumerateFileSystemInfos();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<FileSystemInfo>();
        }
        catch (DirectoryNotFoundException)
        {
            return Array.Empty<FileSystemInfo>();
        }
    }

    private static long ComputeDirectorySize(string absolutePath, CancellationToken cancellationToken)
    {
        var total = 0L;
        var stack = new Stack<string>();
        stack.Push(absolutePath);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = stack.Pop();

            foreach (var child in SafeEnumerate(current))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if ((child.Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;

                if (child is FileInfo file)
                    total += file.Length;
                else if (child is DirectoryInfo subDir)
                    stack.Push(subDir.FullName);
            }
        }

        return total;
    }

    private static void DoMove(string fromAbsolute, string toAbsolute)
    {
        if (Directory.Exists(fromAbsolute))
        {
            // .NET has no atomic directory-move-with-overwrite; remove the destination first.
            if (Directory.Exists(toAbsolute))
                Directory.Delete(toAbsolute, recursive: true);
            else if (File.Exists(toAbsolute))
                File.Delete(toAbsolute);
            Directory.Move(fromAbsolute, toAbsolute);
            return;
        }

        File.Move(fromAbsolute, toAbsolute, overwrite: true);
    }

    private static void DoCopy(string fromAbsolute, string toAbsolute, bool overwrite)
    {
        if (Directory.Exists(fromAbsolute))
            CopyDirectoryRecursive(fromAbsolute, toAbsolute, overwrite);
        else
            File.Copy(fromAbsolute, toAbsolute, overwrite);
    }

    private static void CopyDirectoryRecursive(string source, string dest, bool overwrite)
    {
        if (Directory.Exists(dest) && overwrite)
            Directory.Delete(dest, recursive: true);

        Directory.CreateDirectory(dest);

        // Strict enumeration: a partial copy is worse than a clear failure,
        // so don't use SafeEnumerate here.
        foreach (var child in new DirectoryInfo(source).EnumerateFileSystemInfos())
        {
            if ((child.Attributes & FileAttributes.ReparsePoint) != 0)
                continue;

            var target = Path.Combine(dest, child.Name);
            if (child is DirectoryInfo)
                CopyDirectoryRecursive(child.FullName, target, overwrite);
            else
                File.Copy(child.FullName, target, overwrite);
        }
    }

    private static bool IsSelfOrDescendant(string ancestor, string candidate)
    {
        if (candidate.Equals(ancestor, StringComparison.Ordinal))
            return true;

        var sep = Path.DirectorySeparatorChar;
        var prefix = ancestor.EndsWith(sep) ? ancestor : ancestor + sep;
        return candidate.StartsWith(prefix, StringComparison.Ordinal);
    }

    private static bool IsHidden(FileSystemInfo info)
    {
        if (info.Name.StartsWith('.'))
            return true;
        return (info.Attributes & FileAttributes.Hidden) != 0;
    }

    private static int CompareEntries(FileEntry a, FileEntry b)
    {
        if (a.Type != b.Type)
            return TypeOrder(a.Type).CompareTo(TypeOrder(b.Type));
        return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static int TypeOrder(EntryType type) => type switch
    {
        EntryType.Directory => 0,
        EntryType.File => 1,
        EntryType.Symlink => 2,
        _ => 3
    };
}
