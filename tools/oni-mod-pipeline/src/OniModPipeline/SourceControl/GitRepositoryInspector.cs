using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;
using LibGit2Sharp;

namespace MaksymShostak.OniModPipeline.SourceControl;

internal sealed record GitProvenance(
    string WorktreeRoot,
    string Commit,
    IReadOnlyList<string> ContributingPaths,
    IReadOnlyList<string> DirtyPaths)
{
    internal bool IsClean => DirtyPaths.Count == 0;
}

internal sealed class GitRepositoryInspector
{
    internal Task<OperationResult<GitProvenance>> InspectAsync(
        string workingDirectory,
        IReadOnlyList<string> contributingAbsolutePaths,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(contributingAbsolutePaths);

        return InspectAsync(
            workingDirectory,
            (worktreeRoot, _) => Task.FromResult(ResolveContributingPaths(
                worktreeRoot,
                contributingAbsolutePaths)),
            cancellationToken);
    }

    internal Task<OperationResult<GitProvenance>> InspectAsync(
        ModProfile profile,
        string? pipelineExecutablePath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return InspectAsync(
            profile.ModRoot,
            async (worktreeRoot, observedPaths) =>
            {
                var sourceSetResult = await RelevantSourceSet.CreateAsync(
                    profile,
                    worktreeRoot,
                    observedPaths,
                    pipelineExecutablePath, cancellationToken).ConfigureAwait(false);
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
            cancellationToken);
    }

    private static async Task<OperationResult<GitProvenance>> InspectAsync(
        string workingDirectory,
        Func<string, IReadOnlyList<string>, Task<OperationResult<IReadOnlyList<string>>>> resolveContributingPaths,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rootResult = GitWorktreeLocator.RequireRoot(workingDirectory);
        if (!rootResult.IsSuccess)
            return new(null, rootResult.Diagnostics, rootResult.ExitCode);
        var root = rootResult.Value!;
        try
        {
            // Read Git data through the installed library. Provenance must never run an executable,
            // hook, filesystem monitor or filter program selected by a checkout.
            using var repository = new Repository(root);
            var worktree = repository.Info.WorkingDirectory;
            if (repository.Info.IsBare || worktree is null || Path.GetRelativePath(root, worktree) != ".")
                return Failure<GitProvenance>($"Git metadata does not describe the selected worktree '{root}'.");
            var commit = repository.Head.Tip?.Sha;
            if (commit is null)
                return Failure<GitProvenance>($"Git could not resolve a committed HEAD in '{root}'.");

            var indexed = repository.Index.Select(entry => entry.Path).ToHashSet(StringComparer.Ordinal);
            var status = repository.RetrieveStatus(new StatusOptions
            {
                IncludeIgnored = false,
                IncludeUntracked = true,
                RecurseUntrackedDirs = true,
                // Separate additions and deletions preserve both rename endpoints without a similarity scan.
                DetectRenamesInIndex = false,
                DetectRenamesInWorkDir = false
            });
            cancellationToken.ThrowIfCancellationRequested();
            var changed = status.Select(entry => entry.FilePath).ToHashSet(StringComparer.Ordinal);
            // A staged deletion is already absent from the index; an untracked source can still affect a build.
            var candidates = indexed.Union(changed, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var scope = await resolveContributingPaths(root, candidates).ConfigureAwait(false);
            if (!scope.IsSuccess) return new(null, scope.Diagnostics, scope.ExitCode);
            var contributing = scope.Value!.Select(NormalizeGitPath).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var dirty = contributing.Where(path => changed.Contains(path) || !indexed.Contains(path)).ToArray();
            return new(new GitProvenance(root, commit, contributing, dirty), [], PipelineExitCode.Success);
        }
        catch (Exception exception) when (exception is LibGit2SharpException or IOException or UnauthorizedAccessException ||
            exception.GetBaseException() is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        {
            return Failure<GitProvenance>($"Git provenance at '{root}' could not be read: {exception.Message}");
        }
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
            [DiagnosticCatalog.GitProvenanceUnavailable(reason)],
            PipelineExitCode.ReleaseNotReady);
}
