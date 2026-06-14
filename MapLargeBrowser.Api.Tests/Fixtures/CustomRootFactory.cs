using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MapLargeBrowser.Api.Tests.Fixtures;

/// <summary>
/// WebApplicationFactory that sets MAPLARGE_BROWSER_ROOT to a unique temp dir.
/// IsFallback = false, so reset endpoint returns 403 (intended for that test).
/// All other endpoint tests use this fixture against fresh fixture data.
/// </summary>
public sealed class CustomRootFactory : WebApplicationFactory<Program>
{
    public string TestRoot { get; }
        = Path.Combine(Path.GetTempPath(), "maplarge-tests-custom-" + Guid.NewGuid().ToString("N"));

    public CustomRootFactory()
    {
        // Set env var before the host is built. WebApplicationFactory defers
        // Program.Main execution to the first CreateClient/CreateServer call.
        Environment.SetEnvironmentVariable("MAPLARGE_BROWSER_ROOT", TestRoot);
        TestFileSetup.PopulateTestRoot(TestRoot);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }

    /// <summary>Restore the test root to its known starting state.</summary>
    public void ResetTestRoot() => TestFileSetup.PopulateTestRoot(TestRoot);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            Environment.SetEnvironmentVariable("MAPLARGE_BROWSER_ROOT", null);
            try { if (Directory.Exists(TestRoot)) Directory.Delete(TestRoot, recursive: true); }
            catch { /* best effort */ }
        }
    }
}
