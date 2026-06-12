namespace MapLargeBrowser.Api.Services;

public abstract class FileBrowserException : Exception
{
    protected FileBrowserException(string message) : base(message) { }
}

public sealed class InvalidFileBrowserOperationException : FileBrowserException
{
    public InvalidFileBrowserOperationException(string message) : base(message) { }
}

public sealed class FileBrowserConflictException : FileBrowserException
{
    public FileBrowserConflictException(string message) : base(message) { }
}
