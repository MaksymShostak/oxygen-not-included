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
public sealed class CatalogCommandTests
{
    private const string Success = """{"schemaVersion":1,"operation":"check","runtimeVersion":[3,14,8],"success":true,"value":{"keyCount":1,"localeCount":1},"diagnostics":[]}""";
    private const string Rejected = """{"schemaVersion":1,"operation":"check","runtimeVersion":[3,14,8],"success":false,"value":null,"diagnostics":["uk.po: source text"]}""";

    [Test]
    [Arguments("validate")]
    [Arguments("build")]
    public async Task CatalogFailureBlocksCliBeforeArtifactCreation(string command)
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        Declare(fixture.ModRoot);
        var runner = new CatalogProcessRunner(fixture.Services.ProcessRunner, Rejected, 1);
        var services = fixture.Services with { ProcessRunner = runner };
        var before = SourceSnapshot.CaptureTree(fixture.WorktreeRoot);
        var invocation = await DiagnoseCommandTests.InvokeAsync(CliApplication.CreateRootCommand(services),
            fixture.CreateArguments(command, "--python", PythonSelection(), "--format", "json"));
        await Assert.That(invocation.ExitCode).IsEqualTo(2);
        using var output = JsonDocument.Parse(invocation.StandardOutput);
        await Assert.That(output.RootElement.GetProperty("diagnostics")[0].GetProperty("id").GetString()).IsEqualTo("ONIP1010");
        await Assert.That(Directory.Exists(fixture.ArtifactsDirectory)).IsFalse();
        await Assert.That(before.ChangedPathsComparedWith(SourceSnapshot.CaptureTree(fixture.WorktreeRoot)).Count).IsEqualTo(0);
        Assert.NotNull(runner.ScriptPath);
        await Assert.That(File.Exists(runner.ScriptPath)).IsFalse();
    }

    [Test]
    public async Task NativeReleaseGateRunsBeforeBuildersOrArtifacts()
    {
        using var fixture = new PreparationFixture();
        var profile = fixture.Request.Profile;
        Declare(profile.ModRoot);
        // The native request boundary does not depend on the CLI's profile resolver.
        profile = profile with { Catalogs = new CatalogsProfile("translations", "example.pot", "Options.cs", "STRINGS.EXAMPLE.OPTIONS.") };
        var request = fixture.Request with { Profile = profile, PythonExecutablePath = Path.Combine(profile.ModRoot, "missing-python.exe") };
        var result = await fixture.Preparer.PrepareAsync(request, CancellationToken.None);
        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.EnvironmentUnavailable);
        await Assert.That(fixture.Trace.Count).IsEqualTo(0);
        await Assert.That(Directory.Exists(fixture.Layout.CandidateDirectory)).IsFalse();
    }

    [Test]
    public async Task InspectionNeedsNoGameEnvironmentAndExportsRawBlocks()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        Declare(fixture.ModRoot);
        var json = """{"schemaVersion":1,"operation":"inspect","runtimeVersion":[3,14,8],"success":true,"value":{"uk.po":{"SAVE":{"block":"literal block","en":"Save","translation":"Зберегти"}}},"diagnostics":[]}""";
        var runner = new CatalogProcessRunner(fixture.Services.ProcessRunner, json, 0);
        var result = await DiagnoseCommandTests.InvokeAsync(CliApplication.CreateRootCommand(fixture.Services with { ProcessRunner = runner }),
            ["inspect-catalogs", "--mod", fixture.ModRoot, "--python", PythonSelection(), "--format", "json"]);
        await Assert.That(result.ExitCode).IsEqualTo(0);
        using var output = JsonDocument.Parse(result.StandardOutput);
        await Assert.That(output.RootElement.GetProperty("value").GetProperty("uk.po").GetProperty("SAVE").GetProperty("block").GetString()).IsEqualTo("literal block");
        await Assert.That(Directory.Exists(fixture.ArtifactsDirectory)).IsFalse();
    }

    [Test]
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
        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.InvalidInput);
        await Assert.That(fixture.Trace.Count).IsEqualTo(0);
        await Assert.That(Directory.Exists(fixture.Layout.CandidateDirectory)).IsFalse();
        await Assert.That(File.Exists(runner.ScriptPath)).IsFalse();
    }

    [Test]
    public async Task SuccessfulGateReportsCountsAndRejectsOldRuntimeAndBadInspectionShape()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        Declare(fixture.ModRoot);
        File.WriteAllBytes(Path.Combine(fixture.ModRoot, "preview.png"), [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
        var runner = new CatalogProcessRunner(fixture.Services.ProcessRunner, Success, 0);
        var invocation = await DiagnoseCommandTests.InvokeAsync(CliApplication.CreateRootCommand(fixture.Services with { ProcessRunner = runner }),
            fixture.CreateArguments("validate", "--python", PythonSelection(), "--format", "json"));
        await Assert.That(invocation.ExitCode).IsEqualTo(0).Because(invocation.StandardOutput + invocation.StandardError);
        using var output = JsonDocument.Parse(invocation.StandardOutput);
        await Assert.That(output.RootElement.GetProperty("value").GetProperty("catalogs").GetProperty("localeCount").GetInt32()).IsEqualTo(1);
        var profile = new ModProfileLoader().Load(Path.Combine(fixture.ModRoot, "oni-mod-pipeline.toml")).Value!;
        foreach (var json in new[] { Success.Replace("[3,14,8]", "[3,9,9]"), Success.Replace("\"keyCount\":1", "\"keyCount\":1.5") })
            await Assert.That((await new CatalogRunner(new CatalogProcessRunner(fixture.Services.ProcessRunner, json, 0))
                .RunAsync(profile, "check", PythonSelection(), CancellationToken.None)).ExitCode).IsEqualTo(PipelineExitCode.EnvironmentUnavailable);
        await Assert.That((await new CatalogRunner(new CatalogProcessRunner(fixture.Services.ProcessRunner,
            Success.Replace("\"check\"", "\"inspect\""), 0)).RunAsync(profile, "inspect", PythonSelection(), CancellationToken.None)).ExitCode).IsEqualTo(PipelineExitCode.EnvironmentUnavailable);
    }

    [Test]
    [Arguments("not json", 0)]
    [Arguments(Success, 1)]
    [Arguments(Rejected, 0)]
    [Arguments("{\"schemaVersion\":1}", 0)]
    public async Task UntrustedBackendResultsFailClosed(string json, int exit)
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        Declare(fixture.ModRoot);
        var profile = new ModProfileLoader().Load(Path.Combine(fixture.ModRoot, "oni-mod-pipeline.toml")).Value!;
        var runner = new CatalogProcessRunner(fixture.Services.ProcessRunner, json, exit);
        var result = await new CatalogRunner(runner).RunAsync(profile, "check", PythonSelection(), CancellationToken.None);
        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.EnvironmentUnavailable);
        await Assert.That(File.Exists(runner.ScriptPath)).IsFalse();
    }

    [Test]
    public async Task DeadlineAndCallerCancellationCleanCompletedBackendCopies()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        Declare(fixture.ModRoot);
        var profile = new ModProfileLoader().Load(Path.Combine(fixture.ModRoot, "oni-mod-pipeline.toml")).Value!;
        var runner = new CatalogProcessRunner(fixture.Services.ProcessRunner, Success, 0, waitForCancellation: true);
        var result = await new CatalogRunner(runner, TimeSpan.FromMilliseconds(100)).RunAsync(profile, "check", PythonSelection(), CancellationToken.None);
        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.EnvironmentUnavailable);
        await Assert.That(File.Exists(runner.ScriptPath)).IsFalse();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.That(async () => {await new CatalogRunner(runner).RunAsync(profile, "check", PythonSelection(), cancellation.Token);}).Throws<OperationCanceledException>();
        await Assert.That(File.Exists(runner.ScriptPath)).IsFalse();
    }

    [Test]
    public async Task NoDeclarationRequiresNoPythonAndNoInspectionResult()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var profile = new ModProfileLoader().Load(Path.Combine(fixture.ModRoot, "oni-mod-pipeline.toml")).Value!;
        var runner = new CatalogProcessRunner(fixture.Services.ProcessRunner, "", 42);
        await Assert.That((await new CatalogRunner(runner).RunAsync(profile, "check", "nonexistent", CancellationToken.None)).IsSuccess).IsTrue();
        await Assert.That(runner.ScriptPath).IsNull();
        var invocation = await DiagnoseCommandTests.InvokeAsync(CliApplication.CreateRootCommand(fixture.Services), ["inspect-catalogs", "--mod", fixture.ModRoot, "--format", "json"]);
        await Assert.That(invocation.ExitCode).IsEqualTo(2);
        using var output = JsonDocument.Parse(invocation.StandardOutput);
        await Assert.That(output.RootElement.GetProperty("value").ValueKind).IsEqualTo(JsonValueKind.Null);
    }

    [Test]
    public async Task CatalogInputsContributeEvenWhenNotPackagedAndRejectTraversal()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        Declare(fixture.ModRoot);
        var profile = new ModProfileLoader().Load(Path.Combine(fixture.ModRoot, "oni-mod-pipeline.toml")).Value!;
        var paths = Directory.EnumerateFiles(fixture.ModRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(fixture.WorktreeRoot, path).Replace('\\', '/')).ToArray();
        var result = RelevantSourceSet.Create(profile, fixture.WorktreeRoot, paths, null);
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.WorktreeRelativePaths.ToArray()).Contains("mods/example/translations/example.pot");
        await Assert.That(result.Value!.WorktreeRelativePaths.ToArray()).Contains("mods/example/Options.cs");
        var metadata = new OniMetadataReader().Read(profile).Value!;
        await Assert.That(new ModProfileValidator().Validate(profile, metadata).IsSuccess).IsTrue();
        await Assert.That(new ModProfileValidator().Validate(profile with { Catalogs = profile.Catalogs! with { Directory = "../escape" } }, metadata).IsSuccess).IsFalse();
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
            await Assert.That(Path.IsPathFullyQualified(request.FileName)).IsTrue();
            await Assert.That(request.Arguments[1]).IsEqualTo("-B");
            ScriptPath = request.Arguments[2];
            await Assert.That(await File.ReadAllTextAsync(ScriptPath, cancellationToken)).Contains("class CatalogError");
            if (waitForCancellation) await Task.Delay(Timeout.Infinite, cancellationToken);
            return new ProcessResult(exit, json, "");
        }
    }
}
