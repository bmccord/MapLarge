using MapLargeBrowser.Api.Configuration;
using MapLargeBrowser.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MapLargeBrowser.Api.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminController(
    BrowseRoot browseRoot,
    ISampleSeeder seeder) : ControllerBase
{
    [HttpPost("reset-sample-root")]
    public IActionResult ResetSampleRoot()
    {
        if (!browseRoot.IsFallback)
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "Reset is only available when serving the bundled SampleRoot.");

        seeder.Reset(browseRoot.AbsolutePath);
        return NoContent();
    }
}
