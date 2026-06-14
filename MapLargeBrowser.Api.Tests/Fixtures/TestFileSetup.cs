namespace MapLargeBrowser.Api.Tests.Fixtures;

/// <summary>
/// Populates a directory with a known test tree. Used by CustomRootFactory
/// to give every mutation test a clean, predictable starting state.
/// </summary>
internal static class TestFileSetup
{
    public static void PopulateTestRoot(string rootPath)
    {
        if (Directory.Exists(rootPath))
            Directory.Delete(rootPath, recursive: true);
        Directory.CreateDirectory(rootPath);

        WriteFile(rootPath, "README.txt", "Test root readme.");
        WriteFile(rootPath, "notes.md", "# Notes\n\nalpha\nbravo\ncharlie");
        WriteFile(rootPath, ".hidden-root.txt", "Hidden at root");

        Directory.CreateDirectory(Path.Combine(rootPath, "documents"));
        WriteFile(rootPath, Path.Combine("documents", "intro.txt"), "Welcome to docs.");
        WriteFile(rootPath, Path.Combine("documents", "spec.md"), "# Spec\n\nLorem ipsum.");
        WriteFile(rootPath, Path.Combine("documents", ".private.txt"), "Private note.");

        Directory.CreateDirectory(Path.Combine(rootPath, "code"));
        WriteFile(rootPath, Path.Combine("code", "sample.ts"), "export function greet() {}");

        Directory.CreateDirectory(Path.Combine(rootPath, "empty-folder"));

        Directory.CreateDirectory(Path.Combine(rootPath, "nested", "level1", "level2"));
        WriteFile(rootPath, Path.Combine("nested", "level1", "level2", "deep.txt"), "Deep content.");
    }

    private static void WriteFile(string root, string relativePath, string content)
    {
        var full = Path.Combine(root, relativePath);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(full, content);
    }
}
