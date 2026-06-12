using System.Diagnostics.CodeAnalysis;

namespace MapLargeBrowser.Api.Services;

public interface IPathResolver
{
    bool IsRoot(string absolutePath);

    bool TryResolve(
        string relativePath,
        [NotNullWhen(true)] out string? absolutePath,
        out PathResolutionFailure failure);

    string ToRelative(string absolutePath);
}
