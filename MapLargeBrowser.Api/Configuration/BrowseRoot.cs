namespace MapLargeBrowser.Api.Configuration;

public sealed class BrowseRoot
{
    public const string EnvironmentVariable = "MAPLARGE_BROWSER_ROOT";
    public const string FallbackFolderName = "SampleRoot";

    public string AbsolutePath { get; }
    public bool IsFallback { get; }

    private BrowseRoot(string absolutePath, bool isFallback)
    {
        AbsolutePath = absolutePath;
        IsFallback = isFallback;
    }

    public static BrowseRoot Resolve(IWebHostEnvironment environment)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariable);
        var isFallback = string.IsNullOrWhiteSpace(fromEnvironment);

        var candidate = isFallback
            ? Path.Combine(environment.ContentRootPath, FallbackFolderName)
            : fromEnvironment!;

        var absolute = Path.GetFullPath(candidate);
        Directory.CreateDirectory(absolute);

        return new BrowseRoot(absolute, isFallback);
    }
}
