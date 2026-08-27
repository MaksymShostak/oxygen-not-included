using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.SourceControl;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.SourceControl;

[TestClass]
public sealed class GitRepositoryInspectorTests
{
    [TestMethod]
    public async Task InspectAsync_WhenContributingFileIsModified_ReportsThatFileDirty()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var repositoryPath = await CreateRepositoryAsync(temporaryDirectory);
        var contributingPath = Path.Combine(repositoryPath, "contributing.txt");
        File.WriteAllText(contributingPath, "modified");
        var inspector = new GitRepositoryInspector(new ExternalProcessRunner());

        var result = await inspector.InspectAsync(
            repositoryPath,
            [contributingPath],
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(
            new[] { "contributing.txt" },
            result.Value?.DirtyPaths.ToArray());
        Assert.IsFalse(result.Value?.IsClean);
    }

    [TestMethod]
    public async Task InspectAsync_WhenUnrelatedFileIsModified_RemainsCleanForReleaseScope()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var repositoryPath = await CreateRepositoryAsync(temporaryDirectory);
        var contributingPath = Path.Combine(repositoryPath, "contributing.txt");
        File.WriteAllText(Path.Combine(repositoryPath, "unrelated.txt"), "modified");
        var inspector = new GitRepositoryInspector(new ExternalProcessRunner());

        var result = await inspector.InspectAsync(
            repositoryPath,
            [contributingPath],
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(result.Value?.IsClean);
        Assert.AreEqual(0, result.Value?.DirtyPaths.Count);
    }

    [TestMethod]
    public async Task InspectAsync_WhenContributingFileIsUntracked_ReportsThatFileDirty()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var repositoryPath = await CreateRepositoryAsync(temporaryDirectory);
        var contributingPath = Path.Combine(repositoryPath, "new-input.txt");
        File.WriteAllText(contributingPath, "untracked");
        var inspector = new GitRepositoryInspector(new ExternalProcessRunner());

        var result = await inspector.InspectAsync(
            repositoryPath,
            [contributingPath],
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(
            new[] { "new-input.txt" },
            result.Value?.DirtyPaths.ToArray());
    }

    [TestMethod]
    public async Task InspectAsync_WhenPathIsOutsideWorktree_ReturnsOnip5001()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var repositoryPath = await CreateRepositoryAsync(temporaryDirectory);
        var outsidePath = temporaryDirectory.GetPath("outside.txt");
        File.WriteAllText(outsidePath, "outside");
        var inspector = new GitRepositoryInspector(new ExternalProcessRunner());

        var result = await inspector.InspectAsync(
            repositoryPath,
            [outsidePath],
            CancellationToken.None);

        Assert.AreEqual(PipelineExitCode.ReleaseNotReady, result.ExitCode);
        Assert.AreEqual("ONIP5001", result.Diagnostics.Single().Id);
    }

    [TestMethod]
    public async Task InspectAsync_WhenPorcelainContainsRenameAndCopy_ReportsBothPathsForEachRecord()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var runner = new SequenceProcessRunner(
        [
            new ProcessResult(0, $"{temporaryDirectory.Path}{Environment.NewLine}", string.Empty),
            new ProcessResult(0, $"0123456789abcdef{Environment.NewLine}", string.Empty),
            new ProcessResult(
                0,
                "R  renamed.txt\0original.txt\0C  copied.txt\0source.txt\0",
                string.Empty),
            new ProcessResult(
                0,
                "renamed.txt\0original.txt\0copied.txt\0source.txt\0",
                string.Empty)
        ]);
        var inspector = new GitRepositoryInspector(runner);
        var contributingPaths = new[]
        {
            "renamed.txt",
            "original.txt",
            "copied.txt",
            "source.txt"
        }
        .Select(path => Path.Combine(temporaryDirectory.Path, path))
        .ToArray();

        var result = await inspector.InspectAsync(
            temporaryDirectory.Path,
            contributingPaths,
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(
            new[] { "copied.txt", "original.txt", "renamed.txt", "source.txt" },
            result.Value!.DirtyPaths.ToArray());
        Assert.HasCount(4, runner.Requests);
        CollectionAssert.AreEqual(
            new[] { "rev-parse", "--show-toplevel" },
            runner.Requests[0].Arguments.ToArray());
        CollectionAssert.AreEqual(
            new[] { "rev-parse", "HEAD" },
            runner.Requests[1].Arguments.ToArray());
        CollectionAssert.AreEqual(
            new[] { "status", "--porcelain=v1", "-z", "--untracked-files=all" },
            runner.Requests[2].Arguments.ToArray());
        CollectionAssert.AreEqual(
            new[] { "ls-files", "-z" },
            runner.Requests[3].Arguments.ToArray());
    }

    private static async Task<string> CreateRepositoryAsync(
        TemporaryDirectory temporaryDirectory)
    {
        var repositoryPath = temporaryDirectory.GetPath("repository");
        Directory.CreateDirectory(repositoryPath);
        File.WriteAllText(Path.Combine(repositoryPath, "contributing.txt"), "initial");
        File.WriteAllText(Path.Combine(repositoryPath, "unrelated.txt"), "initial");
        var runner = new ExternalProcessRunner();

        await RunGitAsync(runner, repositoryPath, "init", "--quiet");
        await RunGitAsync(runner, repositoryPath, "config", "user.name", "Pipeline Tests");
        await RunGitAsync(
            runner,
            repositoryPath,
            "config",
            "user.email",
            "pipeline-tests@example.invalid");
        await RunGitAsync(runner, repositoryPath, "config", "commit.gpgsign", "false");
        await RunGitAsync(runner, repositoryPath, "config", "core.autocrlf", "false");
        await RunGitAsync(runner, repositoryPath, "add", "--all");
        await RunGitAsync(runner, repositoryPath, "commit", "--quiet", "--message", "initial");

        return repositoryPath;
    }

    private static async Task RunGitAsync(
        IExternalProcessRunner runner,
        string workingDirectory,
        params string[] arguments)
    {
        var result = await runner.RunAsync(
            new ProcessRequest(
                "git",
                arguments,
                workingDirectory,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["GIT_CONFIG_GLOBAL"] = OperatingSystem.IsWindows() ? "NUL" : "/dev/null",
                    ["GIT_CONFIG_NOSYSTEM"] = "1"
                }),
            CancellationToken.None);

        Assert.AreEqual(
            0,
            result.ExitCode,
            $"git {string.Join(' ', arguments)} failed: {result.StandardError}");
    }

    private sealed class SequenceProcessRunner(IEnumerable<ProcessResult> results)
        : IExternalProcessRunner
    {
        private readonly Queue<ProcessResult> results = new(results);

        internal List<ProcessRequest> Requests { get; } = [];

        public Task<ProcessResult> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(results.Dequeue());
        }
    }
}
