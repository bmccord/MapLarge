using System.Diagnostics.CodeAnalysis;
using MapLargeBrowser.Api.Configuration;

namespace MapLargeBrowser.Api.Services;

public sealed class PathResolver(BrowseRoot root) : IPathResolver
{
    public bool IsRoot(string absolutePath) =>
        absolutePath.Equals(root.AbsolutePath, StringComparison.Ordinal);

    public bool TryResolve(
        string relativePath,
        [NotNullWhen(true)] out string? absolutePath,
        out PathResolutionFailure failure)
    {
        absolutePath = null;
        failure = PathResolutionFailure.None;

        if (string.IsNullOrEmpty(relativePath))
        {
            absolutePath = root.AbsolutePath;
            return true;
        }

        if (Path.IsPathRooted(relativePath) || relativePath.Contains('\0'))
        {
            failure = PathResolutionFailure.InvalidPath;
            return false;
        }

        var candidate = Path.GetFullPath(Path.Combine(root.AbsolutePath, relativePath));

        if (!IsWithinRoot(candidate))
        {
            failure = PathResolutionFailure.OutsideRoot;
            return false;
        }

        if (CrossesSymlink(candidate))
        {
            failure = PathResolutionFailure.CrossesSymlink;
            return false;
        }

        absolutePath = candidate;
        return true;
    }

    public string ToRelative(string absolutePath)
    {
        var relative = Path.GetRelativePath(root.AbsolutePath, absolutePath);
        if (relative == ".") return string.Empty;
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private bool IsWithinRoot(string absolute)
    {
        if (absolute.Equals(root.AbsolutePath, StringComparison.Ordinal))
            return true;

        var sep = Path.DirectorySeparatorChar;
        var prefix = root.AbsolutePath.EndsWith(sep)
            ? root.AbsolutePath
            : root.AbsolutePath + sep;

        return absolute.StartsWith(prefix, StringComparison.Ordinal);
    }

    private bool CrossesSymlink(string absolute)
    {
        if (absolute.Equals(root.AbsolutePath, StringComparison.Ordinal))
            return false;

        var sep = Path.DirectorySeparatorChar;
        var rootPrefix = root.AbsolutePath.EndsWith(sep)
            ? root.AbsolutePath.Length
            : root.AbsolutePath.Length + 1;
        var tail = absolute[rootPrefix..];
        var segments = tail.Split(sep, StringSplitOptions.RemoveEmptyEntries);

        var current = root.AbsolutePath;
        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
            if (!Path.Exists(current))
                return false;

            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                return true;
        }

        return false;
    }
}
