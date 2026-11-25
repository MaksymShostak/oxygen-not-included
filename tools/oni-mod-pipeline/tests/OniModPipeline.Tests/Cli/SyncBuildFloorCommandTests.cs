using System.Text.Json;
using MaksymShostak.OniModPipeline.Cli;
using MaksymShostak.OniModPipeline.Processes;

namespace MaksymShostak.OniModPipeline.Tests.Cli;

public sealed class SyncBuildFloorCommandTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CheckAndWriteUseTheNearestCheckoutWithoutRunningGit(bool useGitFile)
    {
        using var fixture = new CliCommandFixture(sourceIsDirty: false, gitIsAvailable: false);
        var admin = Path.Combine(fixture.WorktreeRoot, ".git");
        if (useGitFile)
            File.WriteAllText(admin, "gitdir: ../elsewhere/worktrees/example\n");
        else
        {
            Directory.CreateDirectory(admin);
            // These bytes are opaque to root discovery. They are deliberately invalid Git configuration.
            File.WriteAllText(Path.Combine(admin, "config"), "not a configuration section [");
        }

        Directory.CreateDirectory(Path.Combine(fixture.RootPath, ".git"));
        WriteCatalog(fixture.RootPath, 654321);
        WriteCatalog(fixture.WorktreeRoot, 765432);
        string metadata = Path.Combine(fixture.ModRoot, "mod_info.yaml");
        string original = File.ReadAllText(metadata);
        var command = CliApplication.CreateRootCommand(fixture.Services with { ProcessRunner = new ExternalProcessRunner() });
        string[] args = ["sync-build-floor", "--mod", Path.Combine(fixture.ModRoot, "oni-mod-pipeline.toml"), "--format", "json"];

        var drift = await DiagnoseCommandTests.InvokeAsync(command, args);
        await Assert.That(drift.ExitCode).IsNotEqualTo(0);
        await Assert.That(File.ReadAllText(metadata)).IsEqualTo(original);
        var written = await DiagnoseCommandTests.InvokeAsync(command, [.. args, "--write"]);
        await Assert.That(written.ExitCode).IsEqualTo(0).Because(written.StandardOutput + written.StandardError);
        await Assert.That(File.ReadAllText(metadata)).IsEqualTo(original.Replace("123456", "765432", StringComparison.Ordinal));
        var check = await DiagnoseCommandTests.InvokeAsync(command, args);
        await Assert.That(check.ExitCode).IsEqualTo(0).Because(check.StandardOutput);
        await Assert.That(fixture.ProcessRunner.Requests).IsEmpty();
    }

    [Test]
    public async Task MissingCatalogAtNestedBoundaryDoesNotUseTheEnclosingCheckout()
    {
        using var fixture = new CliCommandFixture(sourceIsDirty: false, gitIsAvailable: false);
        Directory.CreateDirectory(Path.Combine(fixture.RootPath, ".git"));
        WriteCatalog(fixture.RootPath, 654321);
        Directory.CreateDirectory(Path.Combine(fixture.WorktreeRoot, ".git"));
        var command = CliApplication.CreateRootCommand(fixture.Services with { ProcessRunner = new ExternalProcessRunner() });
        string metadata = Path.Combine(fixture.ModRoot, "mod_info.yaml");
        string original = File.ReadAllText(metadata);
        var result = await DiagnoseCommandTests.InvokeAsync(command, ["sync-build-floor", "--mod", fixture.ModRoot, "--write"]);
        await Assert.That(result.ExitCode).IsNotEqualTo(0);
        await Assert.That(result.StandardError).Contains("oni-references.json");
        await Assert.That(File.ReadAllText(metadata)).IsEqualTo(original);
        await Assert.That(fixture.ProcessRunner.Requests).IsEmpty();
    }

    private static void WriteCatalog(string root, int build)
    {
        var directory = Directory.CreateDirectory(Path.Combine(root, "tools", "ci"));
        string digest = new('a', 64);
        File.WriteAllText(Path.Combine(directory.FullName, "oni-references.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            manifestSha256 = digest,
            package = new { owner = "example", id = "MaksymShostak.ONI.GameReferences", version = $"{build}.0.0", byteLength = 1, sha256 = digest }
        }));
    }
}
