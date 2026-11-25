using MaksymShostak.OniModPipeline.Diagnostics;

namespace MaksymShostak.OniModPipeline.SourceControl;

internal static class GitWorktreeLocator
{
    // The administrative directory (or gitfile for linked worktrees and submodules) marks the
    // boundary. Locating checkout data does not require opening its Git metadata.
    internal static bool HasMarker(DirectoryInfo directory) =>
        Path.Exists(Path.Combine(directory.FullName, ".git"));

    internal static OperationResult<string> FindRoot(string directoryPath)
    {
        for (var directory = new DirectoryInfo(directoryPath); directory is not null; directory = directory.Parent)
        {
            try
            {
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0 || directory.LinkTarget is not null)
                    return UnsafeBoundary(directoryPath, directory.FullName, "checkout lookup crosses a directory link");
                if (HasMarker(directory))
                    return new(directory.FullName, [], PipelineExitCode.Success);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return UnsafeBoundary(directoryPath, directory.FullName, exception.Message);
            }
        }
        return new(null, [], PipelineExitCode.Success);
    }

    internal static OperationResult<string> RequireRoot(string directoryPath)
    {
        var result = FindRoot(directoryPath);
        return result.IsSuccess && result.Value is null
            ? new(null, [DiagnosticCatalog.DirtyReleaseInput($"Could not locate a checkout root from '{directoryPath}'.")],
                PipelineExitCode.ReleaseNotReady)
            : result;
    }

    private static OperationResult<string> UnsafeBoundary(string start, string path, string reason) =>
        new(null, [DiagnosticCatalog.UnsafeProfilePath(start, path, reason)], PipelineExitCode.InvalidInput);
}
