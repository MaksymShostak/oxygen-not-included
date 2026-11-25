using MaksymShostak.OniModPipeline.Diagnostics;

namespace MaksymShostak.OniModPipeline.SourceControl;

internal static class GitWorktreeLocator
{
    // The administrative directory (or gitfile for linked worktrees and submodules) marks the
    // boundary. Locating checkout data does not require opening its Git metadata.
    internal static bool HasMarker(DirectoryInfo directory) =>
        Path.Exists(Path.Combine(directory.FullName, ".git"));

    internal static string? FindRoot(string directoryPath)
    {
        for (var directory = new DirectoryInfo(directoryPath); directory is not null; directory = directory.Parent)
            if (HasMarker(directory))
                return directory.FullName;

        return null;
    }

    internal static OperationResult<string> RequireRoot(string directoryPath) =>
        FindRoot(directoryPath) is { } root
            ? new(root, [], PipelineExitCode.Success)
            : new(null, [DiagnosticCatalog.DirtyReleaseInput($"Could not locate a checkout root from '{directoryPath}'.")],
                PipelineExitCode.ReleaseNotReady);
}
