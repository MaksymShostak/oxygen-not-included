using MaksymShostak.OniModPipeline.Cli;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.EnvironmentDiscovery;
using MaksymShostak.OniModPipeline.ModInstallation;
using MaksymShostak.OniModPipeline.ModProfiles;

namespace MaksymShostak.OniModPipeline.Tests.Cli;

[TestClass]
public sealed class InstallCommandTests
{
    [TestMethod]
    public async Task Install_WhenHelpIsRequested_DocumentsOnlyExactGuardedSourceForms()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var installer = new CapturingModInstaller();
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { ModInstaller = installer });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            ["install", "--help"]);

        Assert.AreEqual(0, invocation.ExitCode);
        Assert.Contains("--candidate", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--mod", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--build-result", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--target", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("dev", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("local", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("latest", invocation.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.AreEqual(0, installer.CallCount);
    }

    [TestMethod]
    public async Task Install_WhenCandidateAndDevTargetAreExplicit_InstallsExactCandidate()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var installer = new CapturingModInstaller();
        var candidateDirectory = Path.Combine(
            fixture.ArtifactsDirectory,
            "release-candidates",
            "Example.Mod",
            "1.2.3",
            "20260827T140302.1234567Z-0123456789abcdef");
        Directory.CreateDirectory(candidateDirectory);
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { ModInstaller = installer });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            CreateCandidateArguments(fixture, candidateDirectory, "dev"));

        Assert.AreEqual(0, invocation.ExitCode);
        Assert.AreEqual(string.Empty, invocation.StandardError);
        Assert.AreEqual(Path.GetFullPath(candidateDirectory), installer.CandidateDirectory);
        Assert.AreEqual(InstallTarget.Dev, installer.Target);
        Assert.IsNotNull(installer.Environment);
        Assert.AreEqual(fixture.UserDataDirectory, installer.Environment.UserDataDirectory);
        Assert.IsNull(installer.Profile);
        Assert.IsNull(installer.BuildResultPath);
        Assert.Contains("Target: dev", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(
            "Candidate receipt written: true",
            invocation.StandardOutput,
            StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task Install_WhenModBuildResultAndLocalTargetAreExplicit_InstallsExactDevelopmentBuild()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var installer = new CapturingModInstaller();
        var buildResultPath = Path.Combine(
            fixture.ArtifactsDirectory,
            "builds",
            "Example.Mod",
            "run",
            "build-result.json");
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { ModInstaller = installer });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            fixture.CreateArguments(
                "install",
                "--build-result",
                buildResultPath,
                "--target",
                "local"));

        Assert.AreEqual(0, invocation.ExitCode);
        Assert.AreEqual(string.Empty, invocation.StandardError);
        Assert.IsNotNull(installer.Profile);
        Assert.AreEqual(fixture.ModRoot, installer.Profile.ModRoot);
        Assert.IsNotNull(installer.Metadata);
        Assert.AreEqual("Example.Mod", installer.Metadata.StaticId);
        Assert.AreEqual(Path.GetFullPath(buildResultPath), installer.BuildResultPath);
        Assert.AreEqual(InstallTarget.Local, installer.Target);
        Assert.IsNull(installer.CandidateDirectory);
        Assert.Contains("Target: local", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(
            "Candidate receipt written: false",
            invocation.StandardOutput,
            StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("none")]
    [DataRow("candidate-and-mod")]
    [DataRow("candidate-and-build-result")]
    [DataRow("mod-only")]
    [DataRow("build-result-only")]
    public async Task Install_WhenSourceFormIsIncompleteOrMixed_RejectsAtParseTime(
        string scenario)
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var installer = new CapturingModInstaller();
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { ModInstaller = installer });
        var candidateDirectory = Path.Combine(fixture.ArtifactsDirectory, "candidate");
        var buildResultPath = Path.Combine(fixture.ArtifactsDirectory, "build-result.json");
        var sourceArguments = scenario switch
        {
            "none" => Array.Empty<string>(),
            "candidate-and-mod" =>
                ["--candidate", candidateDirectory, "--mod", fixture.ModRoot],
            "candidate-and-build-result" =>
                ["--candidate", candidateDirectory, "--build-result", buildResultPath],
            "mod-only" => ["--mod", fixture.ModRoot],
            "build-result-only" => ["--build-result", buildResultPath],
            _ => throw new AssertFailedException($"Unknown scenario '{scenario}'.")
        };

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            [
                "install",
                .. sourceArguments,
                "--target",
                "dev",
                "--game-directory",
                fixture.GameDirectory,
                "--user-data-directory",
                fixture.UserDataDirectory,
                "--artifacts-directory",
                fixture.ArtifactsDirectory
            ]);

        Assert.AreEqual((int)PipelineExitCode.InvalidInput, invocation.ExitCode);
        Assert.Contains(
            "exactly one source form",
            invocation.StandardError,
            StringComparison.OrdinalIgnoreCase);
        Assert.AreEqual(0, installer.CallCount);
    }

    [TestMethod]
    [DataRow("development")]
    [DataRow("steam")]
    [DataRow("Dev")]
    public async Task Install_WhenTargetIsNotCanonical_RejectsAtParseTime(string target)
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var installer = new CapturingModInstaller();
        var candidateDirectory = Path.Combine(fixture.ArtifactsDirectory, "candidate");
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { ModInstaller = installer });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            CreateCandidateArguments(fixture, candidateDirectory, target));

        Assert.AreEqual((int)PipelineExitCode.InvalidInput, invocation.ExitCode);
        Assert.Contains("dev", invocation.StandardError, StringComparison.Ordinal);
        Assert.Contains("local", invocation.StandardError, StringComparison.Ordinal);
        Assert.AreEqual(0, installer.CallCount);
    }

    [TestMethod]
    [DataRow("candidate")]
    [DataRow("mod")]
    [DataRow("build-result")]
    public async Task Install_WhenExplicitSourcePathIsEmpty_RejectsAtParseTime(
        string emptyOption)
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var installer = new CapturingModInstaller();
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { ModInstaller = installer });
        string[] sourceArguments = emptyOption switch
        {
            "candidate" => ["--candidate", string.Empty],
            "mod" =>
                ["--mod", string.Empty, "--build-result", "build-result.json"],
            "build-result" =>
                ["--mod", fixture.ModRoot, "--build-result", string.Empty],
            _ => throw new AssertFailedException($"Unknown option '{emptyOption}'.")
        };

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            ["install", .. sourceArguments, "--target", "dev"]);

        Assert.AreEqual((int)PipelineExitCode.InvalidInput, invocation.ExitCode);
        Assert.Contains("nonempty path", invocation.StandardError, StringComparison.Ordinal);
        Assert.AreEqual(0, installer.CallCount);
    }

    [TestMethod]
    public async Task Install_WhenTargetIsMissing_RejectsAtParseTime()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var installer = new CapturingModInstaller();
        var candidateDirectory = Path.Combine(fixture.ArtifactsDirectory, "candidate");
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { ModInstaller = installer });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            ["install", "--candidate", candidateDirectory]);

        Assert.AreEqual((int)PipelineExitCode.InvalidInput, invocation.ExitCode);
        Assert.Contains("--target dev", invocation.StandardError, StringComparison.Ordinal);
        Assert.Contains("--target local", invocation.StandardError, StringComparison.Ordinal);
        Assert.AreEqual(0, installer.CallCount);
    }

    private static string[] CreateCandidateArguments(
        PipelineCommandFixture fixture,
        string candidateDirectory,
        string target) =>
    [
        "install",
        "--candidate",
        candidateDirectory,
        "--target",
        target,
        "--game-directory",
        fixture.GameDirectory,
        "--user-data-directory",
        fixture.UserDataDirectory,
        "--artifacts-directory",
        fixture.ArtifactsDirectory
    ];
}

internal sealed class CapturingModInstaller : IModInstaller
{
    private const string Digest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    internal int CallCount { get; private set; }

    internal string? CandidateDirectory { get; private set; }

    internal ModProfile? Profile { get; private set; }

    internal OniMetadata? Metadata { get; private set; }

    internal string? BuildResultPath { get; private set; }

    internal InstallTarget? Target { get; private set; }

    internal PipelineEnvironment? Environment { get; private set; }

    public Task<OperationResult<ModInstallationResult>> InstallCandidateAsync(
        string candidateDirectory,
        InstallTarget target,
        PipelineEnvironment environment,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        CandidateDirectory = Path.GetFullPath(candidateDirectory);
        Target = target;
        Environment = environment;
        return Task.FromResult(Success(target, receiptWritten: true));
    }

    public Task<OperationResult<ModInstallationResult>> InstallBuildAsync(
        ModProfile profile,
        OniMetadata metadata,
        string buildResultPath,
        InstallTarget target,
        PipelineEnvironment environment,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        Profile = profile;
        Metadata = metadata;
        BuildResultPath = Path.GetFullPath(buildResultPath);
        Target = target;
        Environment = environment;
        return Task.FromResult(Success(target, receiptWritten: false));
    }

    private static OperationResult<ModInstallationResult> Success(
        InstallTarget target,
        bool receiptWritten) =>
        new(
            new ModInstallationResult(
                "Example.Mod",
                "1.2.3",
                Digest,
                target,
                Path.Combine("installed", target.ToDirectoryName(), "ExampleMod"),
                new DateTimeOffset(2026, 8, 27, 14, 3, 2, TimeSpan.Zero),
                receiptWritten),
            [],
            PipelineExitCode.Success);
}
