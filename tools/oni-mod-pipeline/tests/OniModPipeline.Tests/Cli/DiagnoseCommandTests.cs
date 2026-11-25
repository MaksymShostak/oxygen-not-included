using MaksymShostak.OniModPipeline.Cli;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.EnvironmentDiscovery;
using MaksymShostak.OniModPipeline.ModInstallation;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.ReleaseCandidates;
using MaksymShostak.OniModPipeline.SourceControl;
using MaksymShostak.OniModPipeline.Tests.Fixtures;
using MaksymShostak.OniModPipeline.WorkshopListing;
using System.CommandLine;
using System.Globalization;

namespace MaksymShostak.OniModPipeline.Tests.Cli;
public sealed class DiagnoseCommandTests
{
    [Test]
    public async Task Diagnose_WhenEnvironmentIsValid_PrintsResolvedPathsWithoutCreatingArtifacts()
    {
        using var fixture = new CliCommandFixture(sourceIsDirty: false);
        var before = SourceSnapshot.CaptureTree(fixture.RootPath);
        var command = CliApplication.CreateRootCommand(fixture.Services);

        var invocation = await InvokeAsync(command, fixture.CreateArguments("diagnose"));

        var after = SourceSnapshot.CaptureTree(fixture.RootPath);
        await Assert.That(invocation.ExitCode).IsEqualTo(0);
        await Assert.That(invocation.StandardError).IsEqualTo(string.Empty);
        await Assert.That(invocation.StandardOutput).Contains(fixture.ModRoot);
        await Assert.That(invocation.StandardOutput).Contains(fixture.WorktreeRoot);
        await Assert.That(fixture.ProcessRunner.Requests.Select(request => request.FileName).Distinct().ToArray())
            .IsEquivalentTo(["dotnet"]).Because("Reading Git provenance must not run an external executable.");
        await Assert.That(invocation.StandardOutput).Contains(fixture.GameDirectory);
        await Assert.That(invocation.StandardOutput).Contains(fixture.ManagedDirectory);
        await Assert.That(invocation.StandardOutput).Contains(fixture.UserDataDirectory);
        await Assert.That(invocation.StandardOutput).Contains(fixture.ArtifactsDirectory);
        await Assert.That(invocation.StandardOutput).Contains("10.0.400");
        await Assert.That(invocation.StandardOutput).Contains("123456");
        await Assert.That(invocation.StandardOutput).Contains("Uploader present: true");
        await Assert.That(Directory.Exists(fixture.ArtifactsDirectory)).IsFalse();
        await Assert.That(before.ChangedPathsComparedWith(after).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Diagnose_WhenHelpIsRequested_DocumentsAllFiveCommonOptionsWithoutDiscovery()
    {
        using var fixture = new CliCommandFixture(sourceIsDirty: false);
        var before = SourceSnapshot.CaptureTree(fixture.RootPath);
        var command = CliApplication.CreateRootCommand(fixture.Services);

        var invocation = await InvokeAsync(command, ["diagnose", "--help"]);

        var after = SourceSnapshot.CaptureTree(fixture.RootPath);
        await Assert.That(invocation.ExitCode).IsEqualTo(0);
        await Assert.That(invocation.StandardOutput).Contains("--mod");
        await Assert.That(invocation.StandardOutput).Contains("--game-directory");
        await Assert.That(invocation.StandardOutput).Contains("--user-data-directory");
        await Assert.That(invocation.StandardOutput).Contains("--artifacts-directory");
        await Assert.That(invocation.StandardOutput).Contains("--format");
        await Assert.That(fixture.ProcessRunner.Requests.Count).IsEqualTo(0);
        await Assert.That(before.ChangedPathsComparedWith(after).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Diagnose_WhenCheckoutIsUnavailable_ReportsEnvironmentWithoutWorktreeProvenance()
    {
        using var fixture = new CliCommandFixture(
            sourceIsDirty: false,
            hasRepository: false);
        var before = SourceSnapshot.CaptureTree(fixture.RootPath);
        var command = CliApplication.CreateRootCommand(fixture.Services);

        var invocation = await InvokeAsync(command, fixture.CreateArguments("diagnose"));

        var after = SourceSnapshot.CaptureTree(fixture.RootPath);
        await Assert.That(invocation.ExitCode).IsEqualTo(0);
        await Assert.That(invocation.StandardError).IsEqualTo(string.Empty);
        await Assert.That(invocation.StandardOutput).Contains("Git worktree root: <unavailable>");
        await Assert.That(before.ChangedPathsComparedWith(after).Count).IsEqualTo(0);
    }

    internal static async Task<CommandInvocation> InvokeAsync(
        RootCommand command,
        string[] arguments)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var parseResult = command.Parse(arguments);
        parseResult.InvocationConfiguration.Output = output;
        parseResult.InvocationConfiguration.Error = error;

        int exitCode;
        if (parseResult.Errors.Count > 0)
        {
            foreach (var parseError in parseResult.Errors)
            {
                error.WriteLine(parseError.Message);
            }

            exitCode = (int)PipelineExitCode.InvalidInput;
        }
        else
        {
            exitCode = await parseResult.InvokeAsync();
        }

        return new CommandInvocation(exitCode, output.ToString(), error.ToString());
    }
}

internal sealed record CommandInvocation(
    int ExitCode,
    string StandardOutput,
    string StandardError);

internal sealed class CliCommandFixture : IDisposable
{
    private readonly TemporaryDirectory temporaryDirectory = new();

    internal CliCommandFixture(bool sourceIsDirty, bool hasRepository = true)
    {
        RootPath = temporaryDirectory.Path;
        WorktreeRoot = temporaryDirectory.GetPath("repository");
        ModRoot = Path.Combine(WorktreeRoot, "mods", "example");
        GameDirectory = temporaryDirectory.GetPath("game");
        ManagedDirectory = Path.Combine(
            GameDirectory,
            "OxygenNotIncluded_Data",
            "Managed");
        UserDataDirectory = temporaryDirectory.GetPath("user-data");
        ArtifactsDirectory = temporaryDirectory.GetPath("pipeline-artifacts");
        WriteProfile();
        WriteEnvironment();

        if (hasRepository)
        {
            TestGitRepository.CommitAll(WorktreeRoot);
            if (sourceIsDirty) File.AppendAllText(Path.Combine(ModRoot, "description.bbcode"), "dirty input\n");
        }
        ProcessRunner = new CliProcessRunner();
        var candidateSource = new GameInstallationCandidateSource(
            HostOperatingSystem.Windows,
            temporaryDirectory.GetPath("home"),
            temporaryDirectory.GetPath("documents"),
            []);
        var gitRepositoryInspector = new GitRepositoryInspector();
        Services = new PipelineServices(
            new ModProfileLocator(),
            new ModProfileLoader(),
            new ModProfileValidator(),
            new OniMetadataReader(),
            new EnvironmentDiscoveryService(
                ProcessRunner,
                new EnvironmentVariableSource(
                    new Dictionary<string, string?>()),
                candidateSource,
                new SteamLibraryCatalog()),
            gitRepositoryInspector,
            new WorkshopListingValidator(),
            ReleaseCandidatePreparer.CreateDefault(
                ProcessRunner,
                gitRepositoryInspector),
            ModInstaller.CreateDefault(),
            AcceptanceRecorder.CreateDefault(new SystemAcceptanceConsole()),
            ReleaseCandidateVerifier.CreateDefault(),
            ProcessRunner);
    }

    internal string RootPath { get; }

    internal string WorktreeRoot { get; }

    internal string ModRoot { get; }

    internal string GameDirectory { get; }

    internal string ManagedDirectory { get; }

    internal string UserDataDirectory { get; }

    internal string ArtifactsDirectory { get; }

    internal CliProcessRunner ProcessRunner { get; }

    internal PipelineServices Services { get; }

    internal string[] CreateArguments(string command, params string[] additionalArguments) =>
    [
        command,
        "--mod",
        ModRoot,
        "--game-directory",
        GameDirectory,
        "--user-data-directory",
        UserDataDirectory,
        "--artifacts-directory",
        ArtifactsDirectory,
        .. additionalArguments
    ];

    public void Dispose() => temporaryDirectory.Dispose();

    private void WriteProfile()
    {
        Directory.CreateDirectory(ModRoot);
        File.WriteAllText(
            Path.Combine(ModRoot, "oni-mod-pipeline.toml"),
            """
            schema-version = 1

            [mod]
            mod-yaml = "mod.yaml"
            mod-info-yaml = "mod_info.yaml"

            [[package-files]]
            source = "mod.yaml"
            destination = "mod.yaml"

            [[package-files]]
            source = "mod_info.yaml"
            destination = "mod_info.yaml"

            [workshop-listing]
            description = "description.bbcode"
            change-notes = "change-notes.bbcode"
            preview = "preview.png"
            mod-types = ["tweaks"]
            dlc-compatibility = ["base-game"]

            [local-install]
            directory-name = "ExampleMod"
            """);
        File.WriteAllText(
            Path.Combine(ModRoot, "mod.yaml"),
            """
            title: Example Mod
            description: Example description
            staticID: Example.Mod
            """);
        File.WriteAllText(
            Path.Combine(ModRoot, "mod_info.yaml"),
            """
            supportedContent: ALL
            minimumSupportedBuild: 123456
            version: 1.2.3
            APIVersion: 2
            """);
        File.WriteAllText(Path.Combine(ModRoot, "description.bbcode"), "Description\n");
        File.WriteAllText(Path.Combine(ModRoot, "change-notes.bbcode"), "Changes\n");
        File.WriteAllBytes(
            Path.Combine(ModRoot, "preview.png"),
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
    }

    private void WriteEnvironment()
    {
        Directory.CreateDirectory(ManagedDirectory);
        Directory.CreateDirectory(UserDataDirectory);
        File.WriteAllText(
            Path.Combine(ManagedDirectory, "Assembly-CSharp.dll"),
            "assembly");
        File.WriteAllText(Path.Combine(ManagedDirectory, "0Harmony.dll"), "harmony");
        File.WriteAllText(
            Path.Combine(GameDirectory, "build.json"),
            "{\"build\":123456}\n");
        File.WriteAllText(Path.Combine(GameDirectory, "OniUploader64.exe"), "uploader");
    }
}

internal sealed class CliProcessRunner : IExternalProcessRunner
{
    internal List<ProcessRequest> Requests { get; } = [];

    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        await Assert.That(request.FileName).IsEqualTo("dotnet");
        await Assert.That(request.Arguments.ToArray()).IsEquivalentTo(new[] { "--version" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        return new ProcessResult(0, $"10.0.400{Environment.NewLine}", string.Empty);
    }
}
