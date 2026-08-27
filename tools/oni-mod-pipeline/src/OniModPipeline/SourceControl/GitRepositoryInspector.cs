using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Processes;
using System.ComponentModel;

namespace MaksymShostak.OniModPipeline.SourceControl;

internal sealed record GitProvenance(
    string WorktreeRoot,
    string Commit,
    IReadOnlyList<string> ContributingPaths,
    IReadOnlyList<string> DirtyPaths)
{
    internal bool IsClean => DirtyPaths.Count == 0;
}

internal sealed class GitRepositoryInspector(IExternalProcessRunner processRunner)
{
    internal async Task<OperationResult<GitProvenance>> InspectAsync(
        string workingDirectory,
        IReadOnlyList<string> contributingAbsolutePaths,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(contributingAbsolutePaths);

        return await InspectAsync(
            workingDirectory,
            (worktreeRoot, _) => ResolveContributingPaths(
                worktreeRoot,
                contributingAbsolutePaths),
            cancellationToken).ConfigureAwait(false);
    }

    internal async Task<OperationResult<GitProvenance>> InspectAsync(
        ModProfile profile,
        string? pipelineExecutablePath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return await InspectAsync(
            profile.ModRoot,
            (worktreeRoot, trackedPaths) =>
            {
                var sourceSetResult = RelevantSourceSet.Create(
                    profile,
                    worktreeRoot,
                    trackedPaths,
                    pipelineExecutablePath);
                return sourceSetResult.IsSuccess
                    ? new OperationResult<IReadOnlyList<string>>(
                        sourceSetResult.Value!.WorktreeRelativePaths,
                        [],
                        PipelineExitCode.Success)
                    : new OperationResult<IReadOnlyList<string>>(
                        null,
                        sourceSetResult.Diagnostics,
                        sourceSetResult.ExitCode);
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<OperationResult<GitProvenance>> InspectAsync(
        string workingDirectory,
        Func<string, IReadOnlyList<string>, OperationResult<IReadOnlyList<string>>>
            resolveContributingPaths,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var rootResult = await RunGitAsync(
            workingDirectory,
            ["rev-parse", "--show-toplevel"],
            cancellationToken).ConfigureAwait(false);
        if (rootResult.ExitCode != 0)
        {
            return Failure<GitProvenance>(
                $"Git could not locate a worktree from '{workingDirectory}': {rootResult.StandardError}");
        }

        var worktreeRoot = Path.GetFullPath(rootResult.StandardOutput.TrimEnd('\r', '\n'));
        var commitResult = await RunGitAsync(
            worktreeRoot,
            ["rev-parse", "HEAD"],
            cancellationToken).ConfigureAwait(false);
        if (commitResult.ExitCode != 0)
        {
            return Failure<GitProvenance>(
                $"Git could not resolve HEAD: {commitResult.StandardError}");
        }

        var statusResult = await RunGitAsync(
            worktreeRoot,
            ["status", "--porcelain=v1", "-z", "--untracked-files=all"],
            cancellationToken).ConfigureAwait(false);
        if (statusResult.ExitCode != 0)
        {
            return Failure<GitProvenance>(
                $"Git could not inspect worktree status: {statusResult.StandardError}");
        }

        var trackedResult = await RunGitAsync(
            worktreeRoot,
            ["ls-files", "-z"],
            cancellationToken).ConfigureAwait(false);
        if (trackedResult.ExitCode != 0)
        {
            return Failure<GitProvenance>(
                $"Git could not enumerate tracked files: {trackedResult.StandardError}");
        }

        var statusPaths = ParseStatusPaths(statusResult.StandardOutput);
        var trackedPaths = trackedResult.StandardOutput
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeGitPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var contributingResult = resolveContributingPaths(worktreeRoot, trackedPaths);
        if (!contributingResult.IsSuccess)
        {
            return new OperationResult<GitProvenance>(
                null,
                contributingResult.Diagnostics,
                contributingResult.ExitCode);
        }

        var normalizedContributingPaths = contributingResult.Value!
            .Select(NormalizeGitPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var trackedPathSet = trackedPaths.ToHashSet(StringComparer.Ordinal);
        var dirtyPaths = normalizedContributingPaths
            .Where(path => statusPaths.Contains(path) || !trackedPathSet.Contains(path))
            .ToArray();

        var provenance = new GitProvenance(
            worktreeRoot,
            commitResult.StandardOutput.TrimEnd('\r', '\n'),
            normalizedContributingPaths,
            dirtyPaths);
        return new OperationResult<GitProvenance>(
            provenance,
            [],
            PipelineExitCode.Success);
    }

    private static OperationResult<IReadOnlyList<string>> ResolveContributingPaths(
        string worktreeRoot,
        IReadOnlyList<string> contributingAbsolutePaths)
    {
        var contributingPaths = new List<string>(contributingAbsolutePaths.Count);
        foreach (var absolutePath in contributingAbsolutePaths)
        {
            var fullPath = Path.GetFullPath(absolutePath);
            var relativePath = Path.GetRelativePath(worktreeRoot, fullPath);
            if (relativePath == "." || IsOutsideWorktree(relativePath))
            {
                return Failure<IReadOnlyList<string>>(
                    $"Contributing path '{fullPath}' is outside Git worktree '{worktreeRoot}'.");
            }

            contributingPaths.Add(NormalizeGitPath(relativePath));
        }

        return new OperationResult<IReadOnlyList<string>>(
            contributingPaths,
            [],
            PipelineExitCode.Success);
    }

    private async Task<ProcessResult> RunGitAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            return await processRunner.RunAsync(
                new ProcessRequest(
                    "git",
                    arguments,
                    workingDirectory,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["GIT_CONFIG_GLOBAL"] = OperatingSystem.IsWindows()
                            ? "NUL"
                            : "/dev/null",
                        ["GIT_CONFIG_NOSYSTEM"] = "1",
                        ["GIT_OPTIONAL_LOCKS"] = "0"
                    }),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is Win32Exception or FileNotFoundException)
        {
            return new ProcessResult(-1, string.Empty, exception.Message);
        }
    }

    private static HashSet<string> ParseStatusPaths(string porcelainOutput)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var fields = porcelainOutput.Split('\0');
        for (var index = 0; index < fields.Length; index++)
        {
            var record = fields[index];
            if (record.Length < 4)
            {
                continue;
            }

            paths.Add(NormalizeGitPath(record[3..]));
            var isRenameOrCopy = record[0] is 'R' or 'C' || record[1] is 'R' or 'C';
            if (isRenameOrCopy && index + 1 < fields.Length && fields[index + 1].Length > 0)
            {
                paths.Add(NormalizeGitPath(fields[++index]));
            }
        }

        return paths;
    }

    private static bool IsOutsideWorktree(string relativePath) =>
        Path.IsPathRooted(relativePath) ||
        relativePath == ".." ||
        relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);

    private static string NormalizeGitPath(string path) =>
        path.Replace((char)92, '/');

    private static OperationResult<T> Failure<T>(string reason) =>
        new(
            default,
            [DiagnosticCatalog.DirtyReleaseInput(reason)],
            PipelineExitCode.ReleaseNotReady);
}
