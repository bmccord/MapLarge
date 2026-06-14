using System.Net;
using System.Net.Http.Headers;
using MapLargeBrowser.Api.Tests.Fixtures;

namespace MapLargeBrowser.Api.Tests.Endpoints;

public sealed class UploadTests(CustomRootFactory factory) : CustomRootTestBase(factory)
{
    [Fact]
    public async Task Uploads_new_file_to_root()
    {
        var response = await PostFile("hello.txt", "Hello world", path: "");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(File.Exists(AbsolutePathOnDisk("hello.txt")));
        Assert.Equal("Hello world", await File.ReadAllTextAsync(AbsolutePathOnDisk("hello.txt")));
    }

    [Fact]
    public async Task Uploads_to_subdirectory()
    {
        var response = await PostFile("note.txt", "Subdir note", path: "documents");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(File.Exists(AbsolutePathOnDisk("documents/note.txt")));
    }

    [Fact]
    public async Task Conflict_returns_409_without_overwrite()
    {
        var response = await PostFile("README.txt", "new content", path: "");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Contains("already exists", problem.Detail);

        // original untouched
        Assert.Equal("Test root readme.", await File.ReadAllTextAsync(AbsolutePathOnDisk("README.txt")));
    }

    [Fact]
    public async Task Overwrite_true_replaces_file()
    {
        var response = await PostFile("README.txt", "new content", path: "", overwrite: true);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("new content", await File.ReadAllTextAsync(AbsolutePathOnDisk("README.txt")));
    }

    [Fact]
    public async Task Invalid_filename_returns_400()
    {
        // Filename starting with "." after GetFileName is rejected as "." or ".."
        var response = await PostFile("..", "x", path: "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Contains("Invalid filename", problem.Detail);
    }

    [Fact]
    public async Task Missing_file_in_form_returns_400()
    {
        using var content = new MultipartFormDataContent();
        var response = await Client.PostAsync("/api/upload?path=", content);

        // [ApiController] auto-rejects empty multipart with its own 400 response shape;
        // the controller's explicit "No file uploaded" message only fires when the form
        // parses but no file field is present. Either way, 400 is the contract.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Missing_target_directory_returns_404()
    {
        var response = await PostFile("x.txt", "x", path: "ghost-dir");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Path_escape_returns_400()
    {
        var response = await PostFile("x.txt", "x", path: "../escape");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpResponseMessage> PostFile(
        string filename,
        string content,
        string path,
        bool overwrite = false)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(content));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(fileContent, "file", filename);

        var url = $"/api/upload?path={Uri.EscapeDataString(path)}";
        if (overwrite) url += "&overwrite=true";
        return await Client.PostAsync(url, form);
    }
}
