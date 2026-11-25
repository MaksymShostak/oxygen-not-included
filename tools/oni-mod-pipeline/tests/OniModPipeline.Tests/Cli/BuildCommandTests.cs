using MaksymShostak.OniModPipeline.Cli;
using MaksymShostak.OniModPipeline.EnvironmentDiscovery;
using MaksymShostak.OniModPipeline.ModInstallation;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.ReleaseCandidates;
using MaksymShostak.OniModPipeline.SourceControl;
using MaksymShostak.OniModPipeline.Tests.Fixtures;
using MaksymShostak.OniModPipeline.WorkshopListing;
using System.Text.Json;

namespace MaksymShostak.OniModPipeline.Tests.Cli;
public sealed class BuildCommandTests
{
    [Test]
    public async Task Build_WhenProfileIsContentOnly_PrintsExactBuildResultPath()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var command = CliApplication.CreateRootCommand(fixture.Services);

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            fixture.CreateArguments("build", "--configuration", "Release"));

        await Assert.That(invocation.ExitCode).IsEqualTo(0);
        await Assert.That(invocation.StandardError).IsEqualTo(string.Empty);
        var buildResultPath = invocation.StandardOutput.TrimEnd('\r', '\n');
        await Assert.That(Path.GetFileName(buildResultPath)).IsEqualTo("build-result.json");
        await Assert.That(File.Exists(buildResultPath)).IsTrue();
        await Assert.That(buildResultPath.StartsWith(
            Path.Combine(fixture.ArtifactsDirectory, "builds", "Example.Mod") +
            Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase)).IsTrue();
        using var document = JsonDocument.Parse(await File.ReadAllBytesAsync(buildResultPath));
        await Assert.That(document.RootElement.GetProperty("runRoot").GetString()).IsEqualTo(Path.GetDirectoryName(buildResultPath));
        await Assert.That(document.RootElement
            .GetProperty("primaryOutputPath")
            .ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(document.RootElement.GetProperty("sourceCommit").GetString()).IsEqualTo(fixture.Commit);
        await Assert.That(fixture.ProcessRunner.BuildOrTestRequests.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Build_WhenJsonRequested_ReturnsExactBuildResultPathAsValue()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var command = CliApplication.CreateRootCommand(fixture.Services);

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            fixture.CreateArguments("build", "--format", "json"));

        await Assert.That(invocation.ExitCode).IsEqualTo(0);
        await Assert.That(invocation.StandardError).IsEqualTo(string.Empty);
        using var document = JsonDocument.Parse(invocation.StandardOutput);
        var buildResultPath = document.RootElement.GetProperty("value").GetString();
        Assert.NotNull(buildResultPath);
        await Assert.That(File.Exists(buildResultPath)).IsTrue();
    }
}

internal sealed class PipelineCommandFixture : IDisposable
{
    internal string Commit { get; }

    private readonly TemporaryDirectory temporaryDirectory = new();

    internal PipelineCommandFixture(bool includeTests)
    {
        WorktreeRoot = temporaryDirectory.GetPath("repository");
        ModRoot = Path.Combine(WorktreeRoot, "mods", "example");
        GameDirectory = temporaryDirectory.GetPath("game");
        ManagedDirectory = Path.Combine(
            GameDirectory,
            "OxygenNotIncluded_Data",
            "Managed");
        UserDataDirectory = temporaryDirectory.GetPath("user-data");
        ArtifactsDirectory = temporaryDirectory.GetPath("pipeline-artifacts");
        WriteProfile(includeTests);
        WriteEnvironment();
        Commit = TestGitRepository.CommitAll(WorktreeRoot);
        ProcessRunner = new PipelineCommandProcessRunner();
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

    internal string WorktreeRoot { get; }

    internal string ModRoot { get; }

    internal string GameDirectory { get; }

    internal string ManagedDirectory { get; }

    internal string UserDataDirectory { get; }

    internal string ArtifactsDirectory { get; }

    internal PipelineCommandProcessRunner ProcessRunner { get; }

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

    private void WriteProfile(bool includeTests)
    {
        Directory.CreateDirectory(ModRoot);
        var tests = includeTests
            ? """

              [[test-projects]]
              id = "example-regressions"
              path = "Tests/Example.Tests.csproj"
              required = true
              """
            : string.Empty;
        File.WriteAllText(
            Path.Combine(ModRoot, "oni-mod-pipeline.toml"),
            $$"""
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
            {{tests}}
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
        File.WriteAllBytes(Path.Combine(ModRoot, "preview.png"), [0x89, 0x50, 0x4E, 0x47]);
        if (includeTests)
        {
            var testDirectory = Path.Combine(ModRoot, "Tests");
            Directory.CreateDirectory(testDirectory);
            File.WriteAllText(
                Path.Combine(testDirectory, "Example.Tests.csproj"),
                "<Project />\n");
        }
    }

    private void WriteEnvironment()
    {
        Directory.CreateDirectory(ManagedDirectory);
        Directory.CreateDirectory(UserDataDirectory);
        File.WriteAllText(
            Path.Combine(ManagedDirectory, "Assembly-CSharp.dll"),
            "assembly");
        File.WriteAllText(Path.Combine(ManagedDirectory, "0Harmony.dll"), "harmony");
    }
}

internal sealed class PipelineCommandProcessRunner : IExternalProcessRunner
{
    internal List<ProcessRequest> Requests { get; } = [];

    internal IReadOnlyList<ProcessRequest> BuildOrTestRequests => Requests
        .Where(request =>
            request.FileName == "dotnet" &&
            request.Arguments.Count > 0 &&
            request.Arguments[0] is "restore" or "test" or "build" or "msbuild")
        .ToArray();

    public async Task<ProcessResult> RunAsync(
        ProcessRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        await Assert.That(request.FileName).IsEqualTo("dotnet");
        return RunDotnet(request);
    }

    private static ProcessResult RunDotnet(ProcessRequest request)
    {
        if (request.Arguments.SequenceEqual(["--version"]))
        {
            return new ProcessResult(0, $"10.0.400{Environment.NewLine}", string.Empty);
        }

        if (request.Arguments[0] == "restore")
        {
            return new ProcessResult(0, "restored", string.Empty);
        }

        if (request.Arguments[0] == "test")
        {
            var resultsIndex = FindArgumentIndex(
                request.Arguments,
                "--results-directory");
            var filenameIndex = FindArgumentIndex(
                request.Arguments,
                "--report-trx-filename");
            var trxPath = Path.Combine(
                request.Arguments[resultsIndex + 1],
                request.Arguments[filenameIndex + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(trxPath)!);
            File.WriteAllText(trxPath, "<TestRun />\n");
            return new ProcessResult(0, "tested", string.Empty);
        }

        return new ProcessResult(
            2,
            string.Empty,
            $"Unexpected dotnet arguments: {string.Join(' ', request.Arguments)}");
    }

    private static int FindArgumentIndex(
        IReadOnlyList<string> arguments,
        string expected)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            if (string.Equals(arguments[index], expected, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
