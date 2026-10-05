using System.Text.Json;
using MaksymShostak.OniModPipeline.Catalogs;
using MaksymShostak.OniModPipeline.Cli;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.ReleaseCandidates;
using MaksymShostak.OniModPipeline.SourceControl;
using MaksymShostak.OniModPipeline.Tests.ReleaseCandidates;

namespace MaksymShostak.OniModPipeline.Tests.Cli;

[TestClass]
public sealed class CatalogCommandTests
{
    private const string Success = """{"schemaVersion":1,"operation":"check","runtimeVersion":[3,14,8],"success":true,"value":{"keyCount":1,"localeCount":1},"diagnostics":[]}""";
    private const string Rejected = """{"schemaVersion":1,"operation":"check","runtimeVersion":[3,14,8],"success":false,"value":null,"diagnostics":["uk.po: source text"]}""";

    [TestMethod]
    [DataRow("validate")]
    [DataRow("build")]
    public async Task CatalogFailureBlocksCliBeforeArtifactCreation(string command)
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        Declare(fixture.ModRoot);
        var runner = new CatalogProcessRunner(fixture.Services.ProcessRunner, Rejected, 1);
        var services = fixture.Services with { ProcessRunner = runner };
        var before = SourceSnapshot.CaptureTree(fixture.WorktreeRoot);
        var invocation = await DiagnoseCommandTests.InvokeAsync(CliApplication.CreateRootCommand(services),
            fixture.CreateArguments(command, "--python", PythonSelection(), "--format", "json"));
        Assert.AreEqual(2, invocation.ExitCode);
        using var output = JsonDocument.Parse(invocation.StandardOutput);
        Assert.AreEqual("ONIP1010", output.RootElement.GetProperty("diagnostics")[0].GetProperty("id").GetString());
        Assert.IsFalse(Directory.Exists(fixture.ArtifactsDirectory));
        Assert.AreEqual(0, before.ChangedPathsComparedWith(SourceSnapshot.CaptureTree(fixture.WorktreeRoot)).Count);
        Assert.IsNotNull(runner.ScriptPath);
        Assert.IsFalse(File.Exists(runner.ScriptPath));
    }

    [TestMethod]
    public async Task NativeReleaseGateRunsBeforeBuildersOrArtifacts()
    {
        using var fixture = new PreparationFixture();
        var profile = fixture.Request.Profile;
        Declare(profile.ModRoot);
        // The native request boundary does not depend on the CLI's profile resolver.
        profile = profile with { Catalogs = new CatalogsProfile("translations", "example.pot", "Options.cs", "STRINGS.EXAMPLE.OPTIONS.") };
        var request = fixture.Request with { Profile = profile, PythonExecutablePath = Path.Combine(profile.ModRoot, "missing-python.exe") };
        var result = await fixture.Preparer.PrepareAsync(request, CancellationToken.None);
        Assert.AreEqual(PipelineExitCode.EnvironmentUnavailable, result.ExitCode);
        Assert.AreEqual(0, fixture.Trace.Count);
        Assert.IsFalse(Directory.Exists(fixture.Layout.CandidateDirectory));
    }

    [TestMethod]
    public async Task InspectionNeedsNoGameEnvironmentAndExportsRawBlocks()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        Declare(fixture.ModRoot);
        var json = """{"schemaVersion":1,"operation":"inspect","runtimeVersion":[3,14,8],"success":true,"value":{"uk.po":{"SAVE":{"block":"literal block","en":"Save","translation":"Зберегти"}}},"diagnostics":[]}""";
        var runner = new CatalogProcessRunner(fixture.Services.ProcessRunner, json, 0);
        var result = await DiagnoseCommandTests.InvokeAsync(CliApplication.CreateRootCommand(fixture.Services with { ProcessRunner = runner }),
            ["inspect-catalogs", "--mod", fixture.ModRoot, "--python", PythonSelection(), "--format", "json"]);
        Assert.AreEqual(0, result.ExitCode);
        using var output = JsonDocument.Parse(result.StandardOutput);
        Assert.AreEqual("literal block", output.RootElement.GetProperty("value").GetProperty("uk.po").GetProperty("SAVE").GetProperty("block").GetString());
        Assert.IsFalse(Directory.Exists(fixture.ArtifactsDirectory));
    }

    [TestMethod]
    public async Task InvalidCatalogBlocksNativeReleaseEvenWithoutCli()
    {
        var runner = new CatalogProcessRunner(new ExternalProcessRunner(), Rejected, 1);
        using var fixture = new PreparationFixture(catalogRunner: new CatalogRunner(runner));
        Declare(fixture.ModRoot);
        var request = fixture.Request with
        {
            Profile = fixture.Request.Profile with { Catalogs = new CatalogsProfile("translations", "example.pot", "Options.cs", "STRINGS.EXAMPLE.OPTIONS.") },
            PythonExecutablePath = PythonSelection()
        };
        var result = await fixture.Preparer.PrepareAsync(request, CancellationToken.None);
        Assert.AreEqual(PipelineExitCode.InvalidInput, result.ExitCode);
        Assert.AreEqual(0, fixture.Trace.Count);
        Assert.IsFalse(Directory.Exists(fixture.Layout.CandidateDirectory));
        Assert.IsFalse(File.Exists(runner.ScriptPath));
    }

    [TestMethod]
    public async Task SuccessfulGateReportsCountsAndRejectsOldRuntimeAndBadInspectionShape()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        Declare(fixture.ModRoot);
        File.WriteAllBytes(Path.Combine(fixture.ModRoot, "preview.png"), [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
        var runner = new CatalogProcessRunner(fixture.Services.ProcessRunner, Success, 0);
        var invocation = await DiagnoseCommandTests.InvokeAsync(CliApplication.CreateRootCommand(fixture.Services with { ProcessRunner = runner }),
            fixture.CreateArguments("validate", "--python", PythonSelection(), "--format", "json"));
        Assert.AreEqual(0, invocation.ExitCode, invocation.StandardOutput + invocation.StandardError);
        using var output = JsonDocument.Parse(invocation.StandardOutput);
        Assert.AreEqual(1, output.RootElement.GetProperty("value").GetProperty("catalogs").GetProperty("localeCount").GetInt32());
        var profile = new ModProfileLoader().Load(Path.Combine(fixture.ModRoot, "oni-mod-pipeline.toml")).Value!;
        foreach (var json in new[] { Success.Replace("[3,14,8]", "[3,9,9]"), Success.Replace("\"keyCount\":1", "\"keyCount\":1.5") })
            Assert.AreEqual(PipelineExitCode.EnvironmentUnavailable, (await new CatalogRunner(new CatalogProcessRunner(fixture.Services.ProcessRunner, json, 0))
                .RunAsync(profile, "check", PythonSelection(), CancellationToken.None)).ExitCode);
        Assert.AreEqual(PipelineExitCode.EnvironmentUnavailable, (await new CatalogRunner(new CatalogProcessRunner(fixture.Services.ProcessRunner,
            Success.Replace("\"check\"", "\"inspect\""), 0)).RunAsync(profile, "inspect", PythonSelection(), CancellationToken.None)).ExitCode);
    }

    [TestMethod]
    [DataRow("not json", 0)]
    [DataRow(Success, 1)]
    [DataRow(Rejected, 0)]
    [DataRow("{\"schemaVersion\":1}", 0)]
    public async Task UntrustedBackendResultsFailClosed(string json, int exit)
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        Declare(fixture.ModRoot);
        var profile = new ModProfileLoader().Load(Path.Combine(fixture.ModRoot, "oni-mod-pipeline.toml")).Value!;
        var runner = new CatalogProcessRunner(fixture.Services.ProcessRunner, json, exit);
        var result = await new CatalogRunner(runner).RunAsync(profile, "check", PythonSelection(), CancellationToken.None);
        Assert.AreEqual(PipelineExitCode.EnvironmentUnavailable, result.ExitCode);
        Assert.IsFalse(File.Exists(runner.ScriptPath));
    }

    [TestMethod]
    public async Task DeadlineAndCallerCancellationCleanCompletedBackendCopies()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        Declare(fixture.ModRoot);
        var profile = new ModProfileLoader().Load(Path.Combine(fixture.ModRoot, "oni-mod-pipeline.toml")).Value!;
        var runner = new CatalogProcessRunner(fixture.Services.ProcessRunner, Success, 0, waitForCancellation: true);
        var result = await new CatalogRunner(runner, TimeSpan.FromMilliseconds(100)).RunAsync(profile, "check", PythonSelection(), CancellationToken.None);
        Assert.AreEqual(PipelineExitCode.EnvironmentUnavailable, result.ExitCode);
        Assert.IsFalse(File.Exists(runner.ScriptPath));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAsync<OperationCanceledException>(() => new CatalogRunner(runner).RunAsync(profile, "check", PythonSelection(), cancellation.Token));
        Assert.IsFalse(File.Exists(runner.ScriptPath));
    }

    [TestMethod]
    public async Task NoDeclarationRequiresNoPythonAndNoInspectionResult()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var profile = new ModProfileLoader().Load(Path.Combine(fixture.ModRoot, "oni-mod-pipeline.toml")).Value!;
        var runner = new CatalogProcessRunner(fixture.Services.ProcessRunner, "", 42);
        Assert.IsTrue((await new CatalogRunner(runner).RunAsync(profile, "check", "nonexistent", CancellationToken.None)).IsSuccess);
        Assert.IsNull(runner.ScriptPath);
        var invocation = await DiagnoseCommandTests.InvokeAsync(CliApplication.CreateRootCommand(fixture.Services), ["inspect-catalogs", "--mod", fixture.ModRoot, "--format", "json"]);
        Assert.AreEqual(2, invocation.ExitCode);
        using var output = JsonDocument.Parse(invocation.StandardOutput);
        Assert.AreEqual(JsonValueKind.Null, output.RootElement.GetProperty("value").ValueKind);
    }

    [TestMethod]
    public void CatalogInputsContributeEvenWhenNotPackagedAndRejectTraversal()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        Declare(fixture.ModRoot);
        var profile = new ModProfileLoader().Load(Path.Combine(fixture.ModRoot, "oni-mod-pipeline.toml")).Value!;
        var paths = Directory.EnumerateFiles(fixture.ModRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(fixture.WorktreeRoot, path).Replace('\\', '/')).ToArray();
        var result = RelevantSourceSet.Create(profile, fixture.WorktreeRoot, paths, null);
        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.Contains(result.Value!.WorktreeRelativePaths.ToArray(), "mods/example/translations/example.pot");
        CollectionAssert.Contains(result.Value!.WorktreeRelativePaths.ToArray(), "mods/example/Options.cs");
        var metadata = new OniMetadataReader().Read(profile).Value!;
        Assert.IsTrue(new ModProfileValidator().Validate(profile, metadata).IsSuccess);
        Assert.IsFalse(new ModProfileValidator().Validate(profile with { Catalogs = profile.Catalogs! with { Directory = "../escape" } }, metadata).IsSuccess);
    }

    private static string PythonSelection() => typeof(CatalogCommandTests).Assembly.Location;

    private static void Declare(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "translations"));
        File.WriteAllText(Path.Combine(root, "translations", "example.pot"), "fixture input");
        File.WriteAllText(Path.Combine(root, "Options.cs"), "fixture input");
        File.AppendAllText(Path.Combine(root, "oni-mod-pipeline.toml"), "\n[catalogs]\ndirectory = \"translations\"\ntemplate = \"example.pot\"\noptions-source = \"Options.cs\"\noptions-context-prefix = \"STRINGS.EXAMPLE.OPTIONS.\"\n");
    }

    private sealed class CatalogProcessRunner(IExternalProcessRunner inner, string json, int exit, bool waitForCancellation = false) : IExternalProcessRunner
    {
        internal string? ScriptPath { get; private set; }
        public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            if (!request.Arguments.Contains("-I")) return await inner.RunAsync(request, cancellationToken);
            Assert.IsTrue(Path.IsPathFullyQualified(request.FileName));
            Assert.AreEqual("-B", request.Arguments[1]);
            ScriptPath = request.Arguments[2];
            StringAssert.Contains(await File.ReadAllTextAsync(ScriptPath, cancellationToken), "class CatalogError");
            if (waitForCancellation) await Task.Delay(Timeout.Infinite, cancellationToken);
            return new ProcessResult(exit, json, "");
        }
    }
}
