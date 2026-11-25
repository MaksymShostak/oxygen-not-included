using System.Text.Json;
using MaksymShostak.OniModPipeline.Catalogs;
using MaksymShostak.OniModPipeline.Cli;
using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.Cli;

public sealed class SyncBuildFloorCommandTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CheckAndWriteUseTheNearestCheckoutWithoutRunningGit(bool useGitFile)
    {
        using var fixture = new CliCommandFixture(sourceIsDirty: false, hasRepository: false);
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
        var runner = new CatalogProcessRunner(fixture.WorktreeRoot);
        var command = CliApplication.CreateRootCommand(fixture.Services with { ProcessRunner = runner });
        string[] args = ["sync-build-floor", "--mod", Path.Combine(fixture.ModRoot, "oni-mod-pipeline.toml"), "--format", "json"];

        var drift = await DiagnoseCommandTests.InvokeAsync(command, args);
        await Assert.That(drift.ExitCode).IsNotEqualTo(0);
        await Assert.That(File.ReadAllText(metadata)).IsEqualTo(original);
        var written = await DiagnoseCommandTests.InvokeAsync(command, [.. args, "--write"]);
        await Assert.That(written.ExitCode).IsEqualTo(0).Because(written.StandardOutput + written.StandardError);
        await Assert.That(File.ReadAllText(metadata)).IsEqualTo(original.Replace("123456", "765432", StringComparison.Ordinal));
        var check = await DiagnoseCommandTests.InvokeAsync(command, args);
        await Assert.That(check.ExitCode).IsEqualTo(0).Because(check.StandardOutput);
        await Assert.That(runner.InvocationCount).IsEqualTo(3);
        await Assert.That(fixture.ProcessRunner.Requests).IsEmpty();
    }

    [Test]
    public async Task MissingCatalogAtNestedBoundaryDoesNotUseTheEnclosingCheckout()
    {
        using var fixture = new CliCommandFixture(sourceIsDirty: false, hasRepository: false);
        Directory.CreateDirectory(Path.Combine(fixture.RootPath, ".git"));
        WriteCatalog(fixture.RootPath, 654321);
        Directory.CreateDirectory(Path.Combine(fixture.WorktreeRoot, ".git"));
        var runner = new CatalogProcessRunner(fixture.WorktreeRoot);
        var command = CliApplication.CreateRootCommand(fixture.Services with { ProcessRunner = runner });
        string metadata = Path.Combine(fixture.ModRoot, "mod_info.yaml");
        string original = File.ReadAllText(metadata);
        var result = await DiagnoseCommandTests.InvokeAsync(command, ["sync-build-floor", "--mod", fixture.ModRoot, "--write"]);
        await Assert.That(result.ExitCode).IsNotEqualTo(0);
        await Assert.That(result.StandardError).Contains("oni-references.json");
        await Assert.That(File.ReadAllText(metadata)).IsEqualTo(original);
        await Assert.That(runner.InvocationCount).IsEqualTo(0);
        await Assert.That(fixture.ProcessRunner.Requests).IsEmpty();
    }

    [Test]
    public async Task WriteRejectsAnIntermediateDirectoryLinkBeforeSelectingAnotherCheckoutsCatalog()
    {
        using var fixture = new CliCommandFixture(sourceIsDirty: false, hasRepository: false);
        Directory.CreateDirectory(Path.Combine(fixture.WorktreeRoot, ".git"));
        var outer = Directory.CreateDirectory(Path.Combine(fixture.RootPath, "outer")).FullName;
        Directory.CreateDirectory(Path.Combine(outer, ".git"));
        WriteCatalog(outer, 654321);
        string link = await TestDirectoryLink.CreateAsync(outer, "linked-mods", "../repository/mods");

        try
        {
            var runner = new CatalogProcessRunner(outer);
            var command = CliApplication.CreateRootCommand(fixture.Services with { ProcessRunner = runner });
            string metadata = Path.Combine(fixture.ModRoot, "mod_info.yaml");
            string original = File.ReadAllText(metadata);
            var write = await DiagnoseCommandTests.InvokeAsync(command, ["sync-build-floor", "--mod", Path.Combine(link, "example"), "--write"]);
            await Assert.That(write.ExitCode).IsNotEqualTo(0);
            await Assert.That(File.ReadAllText(metadata)).IsEqualTo(original);
            await Assert.That(runner.InvocationCount).IsEqualTo(0);

            // An unconfigured directory behind the link must not select a manifest above that link.
            foreach (string name in new[] { "oni-mod-pipeline.toml", "mod.yaml", "mod_info.yaml" })
                File.Copy(Path.Combine(fixture.ModRoot, name), Path.Combine(outer, name));
            Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(fixture.ModRoot)!, "unconfigured"));
            var outerWrite = await DiagnoseCommandTests.InvokeAsync(command,
                ["sync-build-floor", "--mod", Path.Combine(link, "unconfigured"), "--write"]);
            await Assert.That(outerWrite.ExitCode).IsNotEqualTo(0);
            await Assert.That(File.ReadAllText(Path.Combine(outer, "mod_info.yaml"))).IsEqualTo(original);
            await Assert.That(runner.InvocationCount).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(link, recursive: false);
        }
    }

    private sealed class CatalogProcessRunner(string root) : IExternalProcessRunner
    {
        internal int InvocationCount { get; private set; }

        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken token)
        {
            string catalog = Path.Combine(root, "tools", "ci", "oni-references.json");
            if (request.FileName != CatalogRunner.ResolvePython(null) || request.StandardInput is null ||
                request.WorkingDirectory != AppContext.BaseDirectory ||
                !request.Arguments.SequenceEqual(["-I", "-B", "-S", "-", "selected-build", "--catalog", catalog]))
                throw new InvalidOperationException("Unexpected child process during build-floor projection.");
            InvocationCount++;
            return new ExternalProcessRunner().RunAsync(request, token);
        }
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
