namespace MapLargeBrowser.Api.Services;

public enum PathResolutionFailure
{
    None,
    InvalidPath,
    OutsideRoot,
    CrossesSymlink
}
