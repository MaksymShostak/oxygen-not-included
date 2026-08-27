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
using System.ComponentModel;
using System.Globalization;

namespace MaksymShostak.OniModPipeline.Tests.Cli;

[TestClass]
public sealed class DiagnoseCommandTests
{
    [TestMethod]
    public async Task Diagnose_WhenEnvironmentIsValid_PrintsResolvedPathsWithoutCreatingArtifacts()
    {
        using var fixture = new CliCommandFixture(sourceIsDirty: false);
        var before = SourceSnapshot.CaptureTree(fixture.RootPath);
        var command = CliApplication.CreateRootCommand(fixture.Services);

        var invocation = await InvokeAsync(command, fixture.CreateArguments("diagnose"));

        var after = SourceSnapshot.CaptureTree(fixture.RootPath);
        Assert.AreEqual(0, invocation.ExitCode);
        Assert.AreEqual(string.Empty, invocation.StandardError);
        StringAssert.Contains(invocation.StandardOutput, fixture.ModRoot);
        StringAssert.Contains(invocation.StandardOutput, fixture.WorktreeRoot);
        StringAssert.Contains(invocation.StandardOutput, fixture.GameDirectory);
        StringAssert.Contains(invocation.StandardOutput, fixture.ManagedDirectory);
        StringAssert.Contains(invocation.StandardOutput, fixture.UserDataDirectory);
        StringAssert.Contains(invocation.StandardOutput, fixture.ArtifactsDirectory);
        StringAssert.Contains(invocation.StandardOutput, "10.0.400");
        StringAssert.Contains(invocation.StandardOutput, "123456");
        StringAssert.Contains(invocation.StandardOutput, "Uploader present: true");
        Assert.IsFalse(Directory.Exists(fixture.ArtifactsDirectory));
        Assert.AreEqual(0, before.ChangedPathsComparedWith(after).Count);
    }

    [TestMethod]
    public async Task Diagnose_WhenHelpIsRequested_DocumentsAllFiveCommonOptionsWithoutDiscovery()
    {
        using var fixture = new CliCommandFixture(sourceIsDirty: false);
        var before = SourceSnapshot.CaptureTree(fixture.RootPath);
        var command = CliApplication.CreateRootCommand(fixture.Services);

        var invocation = await InvokeAsync(command, ["diagnose", "--help"]);

        var after = SourceSnapshot.CaptureTree(fixture.RootPath);
        Assert.AreEqual(0, invocation.ExitCode);
        StringAssert.Contains(invocation.StandardOutput, "--mod");
        StringAssert.Contains(invocation.StandardOutput, "--game-directory");
        StringAssert.Contains(invocation.StandardOutput, "--user-data-directory");
        StringAssert.Contains(invocation.StandardOutput, "--artifacts-directory");
        StringAssert.Contains(invocation.StandardOutput, "--format");
        Assert.AreEqual(0, fixture.ProcessRunner.Requests.Count);
        Assert.AreEqual(0, before.ChangedPathsComparedWith(after).Count);
    }

    [TestMethod]
    public async Task Diagnose_WhenGitIsUnavailable_ReportsEnvironmentWithoutWorktreeProvenance()
    {
        using var fixture = new CliCommandFixture(
            sourceIsDirty: false,
            gitIsAvailable: false);
        var before = SourceSnapshot.CaptureTree(fixture.RootPath);
        var command = CliApplication.CreateRootCommand(fixture.Services);

        var invocation = await InvokeAsync(command, fixture.CreateArguments("diagnose"));

        var after = SourceSnapshot.CaptureTree(fixture.RootPath);
        Assert.AreEqual(0, invocation.ExitCode);
        Assert.AreEqual(string.Empty, invocation.StandardError);
        StringAssert.Contains(
            invocation.StandardOutput,
            "Git worktree root: <unavailable>");
        Assert.AreEqual(0, before.ChangedPathsComparedWith(after).Count);
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

    internal CliCommandFixture(bool sourceIsDirty, bool gitIsAvailable = true)
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

        var trackedPaths = Directory
            .EnumerateFiles(ModRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(WorktreeRoot, path)
                .Replace((char)92, '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var dirtyPath = Path.GetRelativePath(
                WorktreeRoot,
                Path.Combine(ModRoot, "description.bbcode"))
            .Replace((char)92, '/');
        ProcessRunner = new CliProcessRunner(
            WorktreeRoot,
            trackedPaths,
            sourceIsDirty ? dirtyPath : null,
            gitIsAvailable);
        var candidateSource = new GameInstallationCandidateSource(
            HostOperatingSystem.Windows,
            temporaryDirectory.GetPath("home"),
            temporaryDirectory.GetPath("documents"),
            []);
        var gitRepositoryInspector = new GitRepositoryInspector(ProcessRunner);
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

internal sealed class CliProcessRunner(
    string worktreeRoot,
    IReadOnlyList<string> trackedPaths,
    string? dirtyPath,
    bool gitIsAvailable) : IExternalProcessRunner
{
    internal List<ProcessRequest> Requests { get; } = [];

    public Task<ProcessResult> RunAsync(
        ProcessRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        if (string.Equals(request.FileName, "dotnet", StringComparison.Ordinal))
        {
            CollectionAssert.AreEqual(
                new[] { "--version" },
                request.Arguments.ToArray());
            return Task.FromResult(new ProcessResult(
                0,
                $"10.0.400{Environment.NewLine}",
                string.Empty));
        }

        Assert.AreEqual("git", request.FileName);
        if (!gitIsAvailable)
        {
            throw new Win32Exception("git is unavailable for this test.");
        }

        var key = string.Join(' ', request.Arguments);
        return Task.FromResult(key switch
        {
            "rev-parse --show-toplevel" => new ProcessResult(
                0,
                $"{worktreeRoot}{Environment.NewLine}",
                string.Empty),
            "rev-parse HEAD" => new ProcessResult(
                0,
                $"0123456789abcdef0123456789abcdef01234567{Environment.NewLine}",
                string.Empty),
            "status --porcelain=v1 -z --untracked-files=all" => new ProcessResult(
                0,
                dirtyPath is null ? string.Empty : $" M {dirtyPath}\0",
                string.Empty),
            "ls-files -z" => new ProcessResult(
                0,
                string.Join('\0', trackedPaths) + '\0',
                string.Empty),
            _ => new ProcessResult(2, string.Empty, $"Unexpected git arguments: {key}")
        });
    }
}
