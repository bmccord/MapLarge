using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MapLargeBrowser.Api.Tests.Fixtures;

/// <summary>
/// WebApplicationFactory that overrides ContentRootPath to a temp dir.
/// IsFallback = true (no env var), so SampleSeeder runs at startup and
/// reset endpoint succeeds. Used for testing reset and seeded-content behavior.
/// </summary>
public sealed class FallbackRootFactory : WebApplicationFactory<Program>
{
    public string ContentRoot { get; }
        = Path.Combine(Path.GetTempPath(), "maplarge-tests-fallback-" + Guid.NewGuid().ToString("N"));

    public string SampleRoot => Path.Combine(ContentRoot, "SampleRoot");

    public FallbackRootFactory()
    {
        // Ensure no env var leaks in from another fixture or the host.
        Environment.SetEnvironmentVariable("MAPLARGE_BROWSER_ROOT", null);
        Directory.CreateDirectory(ContentRoot);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(ContentRoot);
        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            try { if (Directory.Exists(ContentRoot)) Directory.Delete(ContentRoot, recursive: true); }
            catch { /* best effort */ }
        }
    }
}
