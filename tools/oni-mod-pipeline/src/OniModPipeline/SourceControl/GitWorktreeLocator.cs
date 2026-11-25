using MaksymShostak.OniModPipeline.Diagnostics;

namespace MaksymShostak.OniModPipeline.SourceControl;

internal static class GitWorktreeLocator
{
    // The administrative directory (or gitfile for linked worktrees and submodules) marks the
    // boundary. Locating checkout data does not require opening its Git metadata.
    internal static OperationResult<bool> InspectBoundary(DirectoryInfo directory, string startPath)
    {
        try
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0 || directory.LinkTarget is not null)
                return UnsafeBoundary(startPath, directory.FullName, "checkout lookup crosses a directory link");
            _ = File.GetAttributes(Path.Combine(directory.FullName, ".git"));
            return new(true, [], PipelineExitCode.Success);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new(false, [], PipelineExitCode.Success);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return UnsafeBoundary(startPath, directory.FullName, exception.Message);
        }
    }

    internal static OperationResult<string> FindRoot(string directoryPath)
    {
        for (var directory = new DirectoryInfo(directoryPath); directory is not null; directory = directory.Parent)
        {
            var boundary = InspectBoundary(directory, directoryPath);
            if (!boundary.IsSuccess) return new(null, boundary.Diagnostics, boundary.ExitCode);
            if (boundary.Value) return new(directory.FullName, [], PipelineExitCode.Success);
        }
        return new(null, [], PipelineExitCode.Success);
    }

    internal static OperationResult<string> RequireRoot(string directoryPath)
    {
        var result = FindRoot(directoryPath);
        return result.IsSuccess && result.Value is null
            ? new(null, [DiagnosticCatalog.GitProvenanceUnavailable($"Could not locate a checkout root from '{directoryPath}'.")],
                PipelineExitCode.ReleaseNotReady)
            : result;
    }

    private static OperationResult<bool> UnsafeBoundary(string start, string path, string reason) =>
        new(false, [DiagnosticCatalog.UnsafeProfilePath(start, path, reason)], PipelineExitCode.InvalidInput);
}
