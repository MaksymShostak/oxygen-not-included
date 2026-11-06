using MaksymShostak.OniModPipeline.Cli;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.EnvironmentDiscovery;
using MaksymShostak.OniModPipeline.ModInstallation;
using MaksymShostak.OniModPipeline.ModProfiles;

namespace MaksymShostak.OniModPipeline.Tests.Cli;
public sealed class InstallCommandTests
{
    [Test]
    public async Task Install_WhenHelpIsRequested_DocumentsOnlyExactGuardedSourceForms()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var installer = new CapturingModInstaller();
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { ModInstaller = installer });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            ["install", "--help"]);

        await Assert.That(invocation.ExitCode).IsEqualTo(0);
        await Assert.That(invocation.StandardOutput).Contains("--candidate", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).Contains("--mod", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).Contains("--build-result", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).Contains("--target", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).Contains("dev", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).Contains("local", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).DoesNotContain("latest", StringComparison.OrdinalIgnoreCase);
        await Assert.That(installer.CallCount).IsEqualTo(0);
    }

    [Test]
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

        await Assert.That(invocation.ExitCode).IsEqualTo(0);
        await Assert.That(invocation.StandardError).IsEqualTo(string.Empty);
        await Assert.That(installer.CandidateDirectory).IsEqualTo(Path.GetFullPath(candidateDirectory));
        await Assert.That(installer.Target).IsEqualTo(InstallTarget.Dev);
        Assert.NotNull(installer.Environment);
        await Assert.That(installer.Environment.UserDataDirectory).IsEqualTo(fixture.UserDataDirectory);
        await Assert.That(installer.Profile).IsNull();
        await Assert.That(installer.BuildResultPath).IsNull();
        await Assert.That(invocation.StandardOutput).Contains("Target: dev", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).Contains("Candidate receipt written: true", StringComparison.Ordinal);
    }

    [Test]
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

        await Assert.That(invocation.ExitCode).IsEqualTo(0);
        await Assert.That(invocation.StandardError).IsEqualTo(string.Empty);
        Assert.NotNull(installer.Profile);
        await Assert.That(installer.Profile.ModRoot).IsEqualTo(fixture.ModRoot);
        Assert.NotNull(installer.Metadata);
        await Assert.That(installer.Metadata.StaticId).IsEqualTo("Example.Mod");
        await Assert.That(installer.BuildResultPath).IsEqualTo(Path.GetFullPath(buildResultPath));
        await Assert.That(installer.Target).IsEqualTo(InstallTarget.Local);
        await Assert.That(installer.CandidateDirectory).IsNull();
        await Assert.That(invocation.StandardOutput).Contains("Target: local", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).Contains("Candidate receipt written: false", StringComparison.Ordinal);
    }

    [Test]
    [Arguments("none")]
    [Arguments("candidate-and-mod")]
    [Arguments("candidate-and-build-result")]
    [Arguments("mod-only")]
    [Arguments("build-result-only")]
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
            _ => throw new TUnit.Assertions.Exceptions.AssertionException($"Unknown scenario '{scenario}'.")
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

        await Assert.That(invocation.ExitCode).IsEqualTo((int)PipelineExitCode.InvalidInput);
        await Assert.That(invocation.StandardError).Contains("exactly one source form", StringComparison.OrdinalIgnoreCase);
        await Assert.That(installer.CallCount).IsEqualTo(0);
    }

    [Test]
    [Arguments("development")]
    [Arguments("steam")]
    [Arguments("Dev")]
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

        await Assert.That(invocation.ExitCode).IsEqualTo((int)PipelineExitCode.InvalidInput);
        await Assert.That(invocation.StandardError).Contains("dev", StringComparison.Ordinal);
        await Assert.That(invocation.StandardError).Contains("local", StringComparison.Ordinal);
        await Assert.That(installer.CallCount).IsEqualTo(0);
    }

    [Test]
    [Arguments("candidate")]
    [Arguments("mod")]
    [Arguments("build-result")]
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
            _ => throw new TUnit.Assertions.Exceptions.AssertionException($"Unknown option '{emptyOption}'.")
        };

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            ["install", .. sourceArguments, "--target", "dev"]);

        await Assert.That(invocation.ExitCode).IsEqualTo((int)PipelineExitCode.InvalidInput);
        await Assert.That(invocation.StandardError).Contains("nonempty path", StringComparison.Ordinal);
        await Assert.That(installer.CallCount).IsEqualTo(0);
    }

    [Test]
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

        await Assert.That(invocation.ExitCode).IsEqualTo((int)PipelineExitCode.InvalidInput);
        await Assert.That(invocation.StandardError).Contains("--target dev", StringComparison.Ordinal);
        await Assert.That(invocation.StandardError).Contains("--target local", StringComparison.Ordinal);
        await Assert.That(installer.CallCount).IsEqualTo(0);
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
