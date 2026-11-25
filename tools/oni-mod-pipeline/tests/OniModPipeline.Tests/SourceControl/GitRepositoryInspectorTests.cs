using MaksymShostak.OniModPipeline.Diagnostics;
using LibGit2Sharp;
using MaksymShostak.OniModPipeline.SourceControl;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.SourceControl;
public sealed class GitRepositoryInspectorTests
{
    [Test]
    public async Task Inspect_WhenContributingFileIsModified_ReportsThatFileDirty()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var repositoryPath = CreateRepository(temporaryDirectory);
        var contributingPath = Path.Combine(repositoryPath, "contributing.txt");
        File.WriteAllText(contributingPath, "modified");
        var inspector = new GitRepositoryInspector();

        var result = await inspector.InspectAsync(
            repositoryPath,
            [contributingPath],
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value?.DirtyPaths.ToArray()).IsEquivalentTo(new[] { "contributing.txt" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(result.Value?.IsClean).IsFalse();
    }

    [Test]
    public async Task Inspect_WhenUnrelatedFileIsModified_RemainsCleanForReleaseScope()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var repositoryPath = CreateRepository(temporaryDirectory);
        var contributingPath = Path.Combine(repositoryPath, "contributing.txt");
        File.WriteAllText(Path.Combine(repositoryPath, "unrelated.txt"), "modified");
        var inspector = new GitRepositoryInspector();

        var result = await inspector.InspectAsync(
            repositoryPath,
            [contributingPath],
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value?.IsClean).IsTrue();
        await Assert.That(result.Value?.DirtyPaths.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Inspect_WhenContributingFileIsUntracked_ReportsThatFileDirty()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var repositoryPath = CreateRepository(temporaryDirectory);
        var contributingPath = Path.Combine(repositoryPath, "new-input.txt");
        File.WriteAllText(contributingPath, "untracked");
        var inspector = new GitRepositoryInspector();

        var result = await inspector.InspectAsync(
            repositoryPath,
            [contributingPath],
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value?.DirtyPaths.ToArray()).IsEquivalentTo(new[] { "new-input.txt" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Inspect_WhenPathIsOutsideWorktree_ReturnsOnip5001()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var repositoryPath = CreateRepository(temporaryDirectory);
        var outsidePath = temporaryDirectory.GetPath("outside.txt");
        File.WriteAllText(outsidePath, "outside");
        var inspector = new GitRepositoryInspector();

        var result = await inspector.InspectAsync(
            repositoryPath,
            [outsidePath],
            CancellationToken.None);

        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.ReleaseNotReady);
        await Assert.That(result.Diagnostics.Single().Id).IsEqualTo("ONIP5001");
    }

    [Test]
    public async Task Inspect_ReportsBothEndsOfAStagedRenameAndChangesToACopiedSource()
    {
        using var directory = new TemporaryDirectory();
        string root = CreateRepository(directory);
        File.Move(Path.Combine(root, "contributing.txt"), Path.Combine(root, "renamed.txt"));
        File.Copy(Path.Combine(root, "unrelated.txt"), Path.Combine(root, "copied.txt"));
        File.WriteAllText(Path.Combine(root, "unrelated.txt"), "source edited after copying");
        using (var repository = new Repository(root)) Commands.Stage(repository, "*");
        string[] names = ["contributing.txt", "renamed.txt", "copied.txt", "unrelated.txt"];
        var indexFile = Path.Combine(root, ".git", "index");
        byte[] indexBefore = File.ReadAllBytes(indexFile);

        var result = await new GitRepositoryInspector().InspectAsync(root,
            names.Select(name => Path.Combine(root, name)).ToArray(), CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.DirtyPaths.ToArray()).IsEquivalentTo(names);
        await Assert.That(File.ReadAllBytes(indexFile).SequenceEqual(indexBefore)).IsTrue();
    }

    [Test]
    public async Task Inspect_UsesTheActualWorkingTreeForASeparateGitDirectory()
    {
        using var directory = new TemporaryDirectory();
        string root = CreateRepository(directory);
        string admin = Path.Combine(root, ".git");
        Directory.Move(admin, directory.GetPath("admin"));
        File.WriteAllText(admin, "gitdir: ../admin\n");
        var result = await new GitRepositoryInspector().InspectAsync(root, [Path.Combine(root, "contributing.txt")], CancellationToken.None);
        await Assert.That(result.IsSuccess).IsTrue().Because(string.Join(" ", result.Diagnostics.Select(d => d.Evidence)));
        await Assert.That(result.Value!.WorktreeRoot).IsEqualTo(root);
        await Assert.That(result.Value.IsClean).IsTrue();
    }

    [Test]
    public async Task Inspect_UsesTheWorkingFileWhenRepositoryMetadataNamesAnUnavailableMonitor()
    {
        using var directory = new TemporaryDirectory();
        string root = CreateRepository(directory);
        using (var repository = new Repository(root))
            repository.Config.Set("core.fsmonitor", directory.GetPath("absent-monitor"), ConfigurationLevel.Local);

        string file = Path.Combine(root, "contributing.txt");
        File.WriteAllText(file, "updated after the index was written");
        var result = await new GitRepositoryInspector().InspectAsync(root, [file], CancellationToken.None);
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.DirtyPaths.ToArray()).IsEquivalentTo(["contributing.txt"]);
    }

    [Test]
    public async Task Inspect_RefusesMetadataThatSelectsADifferentWorkingTree()
    {
        using var directory = new TemporaryDirectory();
        string root = CreateRepository(directory);
        var other = Directory.CreateDirectory(directory.GetPath("other"));
        using (var repository = new Repository(root))
            repository.Config.Set("core.worktree", other.FullName, ConfigurationLevel.Local);
        var result = await new GitRepositoryInspector().InspectAsync(root, [Path.Combine(root, "contributing.txt")], CancellationToken.None);
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Diagnostics.Single().Id).IsEqualTo("ONIP5001");
    }

    [Test]
    public async Task Inspect_UnsupportedRefStorageReportsTheSupportedReleaseCheckout()
    {
        using var directory = new TemporaryDirectory();
        string root = CreateRepository(directory);
        using (var repository = new Repository(root))
        {
            repository.Config.Set("core.repositoryformatversion", 1, ConfigurationLevel.Local);
            repository.Config.Set("extensions.refstorage", "reftable", ConfigurationLevel.Local);
        }
        var result = await new GitRepositoryInspector().InspectAsync(root, [Path.Combine(root, "contributing.txt")], CancellationToken.None);
        await Assert.That(result.IsSuccess).IsFalse();
        var diagnostic = result.Diagnostics.Single();
        await Assert.That(diagnostic.Id).IsEqualTo("ONIP5001");
        await Assert.That(diagnostic.NextAction).Contains("standard index");
    }

    private static string CreateRepository(TemporaryDirectory directory)
    {
        var root = Directory.CreateDirectory(directory.GetPath("repository")).FullName;
        File.WriteAllText(Path.Combine(root, "contributing.txt"), "first file\n");
        File.WriteAllText(Path.Combine(root, "unrelated.txt"), "another file\n");
        TestGitRepository.CommitAll(root);
        return root;
    }
}
