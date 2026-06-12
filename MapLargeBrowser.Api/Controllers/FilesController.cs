using MapLargeBrowser.Api.Configuration;
using MapLargeBrowser.Api.Models;
using MapLargeBrowser.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MapLargeBrowser.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class FilesController(
    IPathResolver paths,
    BrowseRoot browseRoot,
    IFileBrowser fileBrowser) : ControllerBase
{
    [HttpGet("browse")]
    public ActionResult<BrowseResponse> Browse(
        [FromQuery] string path = "",
        [FromQuery] bool showHidden = false)
    {
        if (!paths.TryResolve(path, out var absolute, out var failure))
            return MapFailure(failure);

        if (!Directory.Exists(absolute))
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Directory not found: {path}");

        var result = fileBrowser.Browse(absolute, showHidden);

        return new BrowseResponse(
            paths.ToRelative(absolute),
            result.Entries,
            result.FileCount,
            result.DirectoryCount,
            result.ImmediateSize,
            browseRoot.IsFallback);
    }

    [HttpGet("search")]
    public ActionResult<SearchResponse> Search(
        [FromQuery] string path = "",
        [FromQuery] string q = "",
        [FromQuery] bool showHidden = false,
        CancellationToken cancellationToken = default)
    {
        if (!paths.TryResolve(path, out var absolute, out var failure))
            return MapFailure(failure);

        if (!Directory.Exists(absolute))
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Directory not found: {path}");

        if (string.IsNullOrEmpty(q))
            return new SearchResponse(Array.Empty<FileEntry>(), 0, 0, 0L, false);

        var result = fileBrowser.Search(absolute, q, showHidden, cancellationToken);
        return new SearchResponse(
            result.Entries,
            result.FileCount,
            result.DirectoryCount,
            result.TotalSize,
            result.Truncated);
    }

    [HttpGet("size")]
    public ActionResult<long> Size(
        [FromQuery] string path = "",
        CancellationToken cancellationToken = default)
    {
        if (!paths.TryResolve(path, out var absolute, out var failure))
            return MapFailure(failure);

        try
        {
            return fileBrowser.GetSize(absolute, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Path not found: {path}");
        }
    }

    [HttpGet("download")]
    public IActionResult Download([FromQuery] string path = "")
    {
        if (!paths.TryResolve(path, out var absolute, out var failure))
            return MapFailure(failure);

        try
        {
            var stream = fileBrowser.OpenForDownload(absolute);
            return File(stream, "application/octet-stream", Path.GetFileName(absolute));
        }
        catch (FileNotFoundException)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: $"File not found: {path}");
        }
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        [FromQuery] string path = "",
        [FromQuery] bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        if (!paths.TryResolve(path, out var absolute, out var failure))
            return MapFailure(failure);

        if (!Directory.Exists(absolute))
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Target directory not found: {path}");

        var file = Request.Form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "No file uploaded.");

        var name = Path.GetFileName(file.FileName);
        if (!IsValidFilename(name))
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "Invalid filename.");

        try
        {
            await using var stream = file.OpenReadStream();
            await fileBrowser.SaveUploadAsync(absolute, name, stream, overwrite, cancellationToken);
            return Created(paths.ToRelative(Path.Combine(absolute, name)), null);
        }
        catch (FileBrowserConflictException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }
    }

    [HttpDelete("entries")]
    public IActionResult Delete(
        [FromQuery] string path = "",
        [FromQuery] bool recursive = false)
    {
        if (!paths.TryResolve(path, out var absolute, out var failure))
            return MapFailure(failure);

        try
        {
            fileBrowser.DeleteEntry(absolute, recursive);
            return NoContent();
        }
        catch (InvalidFileBrowserOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
        catch (FileBrowserConflictException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }
        catch (FileNotFoundException)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Path not found: {path}");
        }
    }

    [HttpPost("entries/move")]
    public IActionResult Move(
        [FromBody] EntryReference reference,
        [FromQuery] bool overwrite = false)
        => MoveOrCopy(reference, overwrite, TransferKind.Move);

    [HttpPost("entries/copy")]
    public IActionResult Copy(
        [FromBody] EntryReference reference,
        [FromQuery] bool overwrite = false)
        => MoveOrCopy(reference, overwrite, TransferKind.Copy);

    private IActionResult MoveOrCopy(EntryReference reference, bool overwrite, TransferKind kind)
    {
        if (!paths.TryResolve(reference.From, out var fromAbs, out var fromFail))
            return MapFailure(fromFail);
        if (!paths.TryResolve(reference.To, out var toAbs, out var toFail))
            return MapFailure(toFail);

        try
        {
            fileBrowser.Transfer(fromAbs, toAbs, overwrite, kind);
            return NoContent();
        }
        catch (InvalidFileBrowserOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
        catch (FileBrowserConflictException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }
        catch (FileNotFoundException)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Source not found: {reference.From}");
        }
    }

    private static bool IsValidFilename(string name)
    {
        return !string.IsNullOrWhiteSpace(name)
            && !name.Contains(Path.DirectorySeparatorChar)
            && !name.Contains(Path.AltDirectorySeparatorChar)
            && name != "." && name != "..";
    }

    private ObjectResult MapFailure(PathResolutionFailure failure) => failure switch
    {
        PathResolutionFailure.InvalidPath =>
            Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Invalid path."),
        PathResolutionFailure.OutsideRoot =>
            Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Path escapes the browse root."),
        PathResolutionFailure.CrossesSymlink =>
            Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Path crosses a symlink boundary."),
        _ => Problem(statusCode: StatusCodes.Status400BadRequest)
    };
}
