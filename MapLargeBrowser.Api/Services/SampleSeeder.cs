namespace MapLargeBrowser.Api.Services;

public sealed class SampleSeeder : ISampleSeeder
{
    private static readonly IReadOnlyList<SampleFile> Files =
    [
        new("README.txt",
            """
            This is the default sample root for MapLargeBrowser. The API serves this directory
            when MAPLARGE_BROWSER_ROOT is not set. Edit/add files freely to test the browser.
            """),
        new("notes.md",
            """
            # Notes

            A markdown file at the root of the sample browse tree.

            - alpha
            - bravo
            - charlie
            """),
        new(".config/settings.json",
            """
            {
              "theme": "dark",
              "recentPaths": ["documents", "code"]
            }
            """),
        new("documents/intro.txt",
            "Welcome to the sample documents folder.\n"),
        new("documents/spec.md",
            """
            # Spec

            Pretend this is a specification document.

            ## Section 1

            Lorem ipsum.

            ## Section 2

            Dolor sit amet.
            """),
        new("documents/.private.txt",
            "Private note that should only appear when \"Show hidden\" is on.\n"),
        new("code/sample.cs",
            """
            namespace Sample;

            public sealed class Greeter
            {
                public string Greet(string name) => $"Hello, {name}!";
            }
            """),
        new("code/sample.ts",
            """
            export function greet(name: string): string {
              return `Hello, ${name}!`;
            }
            """),
        new("images/banner.svg",
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="60" viewBox="0 0 200 60">
              <rect width="200" height="60" fill="#1976d2"/>
              <text x="100" y="36" font-family="sans-serif" font-size="20" fill="#fff" text-anchor="middle">MapLarge</text>
            </svg>
            """)
    ];

    private static readonly IReadOnlyList<string> EmptyDirectories =
    [
        "nested/level1/level2"
    ];

    public bool IsEmpty(string rootPath)
    {
        return !Directory.Exists(rootPath)
            || !Directory.EnumerateFileSystemEntries(rootPath).Any();
    }

    public void Seed(string rootPath)
    {
        Directory.CreateDirectory(rootPath);

        foreach (var file in Files)
        {
            var fullPath = Path.Combine(rootPath, file.RelativePath);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(fullPath, file.Content);
        }

        foreach (var dir in EmptyDirectories)
        {
            Directory.CreateDirectory(Path.Combine(rootPath, dir));
        }
    }

    public void Reset(string rootPath)
    {
        if (Directory.Exists(rootPath))
        {
            foreach (var entry in new DirectoryInfo(rootPath).EnumerateFileSystemInfos())
            {
                if (entry is DirectoryInfo subDir)
                    subDir.Delete(recursive: true);
                else
                    entry.Delete();
            }
        }
        Seed(rootPath);
    }

    private sealed record SampleFile(string RelativePath, string Content);
}
