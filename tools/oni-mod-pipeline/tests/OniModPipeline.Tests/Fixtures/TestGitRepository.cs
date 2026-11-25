using LibGit2Sharp;

namespace MaksymShostak.OniModPipeline.Tests.Fixtures;

internal static class TestGitRepository
{
    internal static string CommitAll(string root)
    {
        if (!Directory.Exists(Path.Combine(root, ".git"))) Repository.Init(root);
        using var repository = new Repository(root);
        Commands.Stage(repository, "*");
        var author = new Signature("Pipeline Tests", "pipeline-tests@example.invalid", DateTimeOffset.UnixEpoch);
        return repository.Commit("Fixture snapshot", author, author).Sha;
    }
}
