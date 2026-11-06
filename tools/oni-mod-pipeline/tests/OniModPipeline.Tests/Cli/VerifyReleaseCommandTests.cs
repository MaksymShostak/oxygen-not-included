using MaksymShostak.OniModPipeline.Cli;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ReleaseCandidates;

namespace MaksymShostak.OniModPipeline.Tests.Cli;
public sealed class VerifyReleaseCommandTests
{
    [Test]
    public async Task VerifyRelease_WhenHelpIsRequested_DocumentsOnlyCandidateAndOutputFormat()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var verifier = new CapturingReleaseCandidateVerifier();
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { ReleaseCandidateVerifier = verifier });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            ["verify-release", "--help"]);

        await Assert.That(invocation.ExitCode).IsEqualTo(0);
        await Assert.That(invocation.StandardOutput).Contains("--candidate", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).Contains("--format", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).Contains("human", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).Contains("json", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).DoesNotContain("--mod", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).DoesNotContain("--tester", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).DoesNotContain("--target", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).DoesNotContain("publish", StringComparison.OrdinalIgnoreCase);
        await Assert.That(verifier.CallCount).IsEqualTo(0);
        await Assert.That(fixture.ProcessRunner.Requests.Count).IsEqualTo(0);
    }

    [Test]
    public async Task VerifyRelease_WhenCandidateIsReady_DelegatesExactAbsolutePathAndRendersHumanState()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var verifier = new CapturingReleaseCandidateVerifier();
        var candidateDirectory = Path.Combine(
            fixture.ArtifactsDirectory,
            "release-candidates",
            "Example.Mod",
            "1.2.3",
            "20260827T140302.1234567Z-0123456789abcdef");
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { ReleaseCandidateVerifier = verifier });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            ["verify-release", "--candidate", candidateDirectory]);

        await Assert.That(invocation.ExitCode).IsEqualTo(0);
        await Assert.That(invocation.StandardError).IsEqualTo(string.Empty);
        await Assert.That(verifier.CandidateDirectory).IsEqualTo(Path.GetFullPath(candidateDirectory));
        await Assert.That(invocation.StandardOutput).Contains("State: ready-for-upload", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).Contains("Irreversibly invalidated: false", StringComparison.Ordinal);
        await Assert.That(verifier.CallCount).IsEqualTo(1);
        await Assert.That(fixture.ProcessRunner.Requests.Count).IsEqualTo(0);
    }

    [Test]
    public async Task VerifyRelease_WhenJsonIsRequested_RendersCanonicalStructuredState()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var verifier = new CapturingReleaseCandidateVerifier();
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { ReleaseCandidateVerifier = verifier });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            [
                "verify-release",
                "--candidate",
                Path.Combine(fixture.ArtifactsDirectory, "candidate"),
                "--format",
                "json"
            ]);

        await Assert.That(invocation.ExitCode).IsEqualTo(0);
        await Assert.That(invocation.StandardError).IsEqualTo(string.Empty);
        await Assert.That(invocation.StandardOutput).Contains("\"state\": \"ready-for-upload\"", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).Contains("\"exitCode\": 0", StringComparison.Ordinal);
        await Assert.That(verifier.CallCount).IsEqualTo(1);
    }

    [Test]
    public async Task VerifyRelease_WhenCandidateIsNotReady_PreservesReleaseNotReadyExit()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var verifier = new CapturingReleaseCandidateVerifier
        {
            Result = new OperationResult<ReleaseReadinessReport>(
                CapturingReleaseCandidateVerifier.CreateReport(
                    ReleaseCandidateState.AwaitingAcceptance),
                [DiagnosticCatalog.RequiredAcceptanceMissing(
                    "Installation and acceptance evidence have not both been recorded.")],
                PipelineExitCode.ReleaseNotReady)
        };
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { ReleaseCandidateVerifier = verifier });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            [
                "verify-release",
                "--candidate",
                Path.Combine(fixture.ArtifactsDirectory, "candidate")
            ]);

        await Assert.That(invocation.ExitCode).IsEqualTo((int)PipelineExitCode.ReleaseNotReady);
        await Assert.That(invocation.StandardError).Contains(DiagnosticIds.RequiredAcceptanceMissing, StringComparison.Ordinal);
        await Assert.That(verifier.CallCount).IsEqualTo(1);
    }

    [Test]
    public async Task VerifyRelease_WhenCandidateOrFormatIsInvalid_RejectsAtParseTime()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var verifier = new CapturingReleaseCandidateVerifier();
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { ReleaseCandidateVerifier = verifier });

        var missing = await DiagnoseCommandTests.InvokeAsync(
            command,
            ["verify-release"]);
        var empty = await DiagnoseCommandTests.InvokeAsync(
            command,
            ["verify-release", "--candidate", string.Empty]);
        var invalidFormat = await DiagnoseCommandTests.InvokeAsync(
            command,
            [
                "verify-release",
                "--candidate",
                Path.Combine(fixture.ArtifactsDirectory, "candidate"),
                "--format",
                "yaml"
            ]);

        await Assert.That(missing.ExitCode).IsEqualTo((int)PipelineExitCode.InvalidInput);
        await Assert.That(missing.StandardError).Contains("--candidate", StringComparison.Ordinal);
        await Assert.That(empty.ExitCode).IsEqualTo((int)PipelineExitCode.InvalidInput);
        await Assert.That(empty.StandardError).Contains("nonempty path", StringComparison.Ordinal);
        await Assert.That(invalidFormat.ExitCode).IsEqualTo((int)PipelineExitCode.InvalidInput);
        await Assert.That(invalidFormat.StandardError).Contains("human", StringComparison.Ordinal);
        await Assert.That(invalidFormat.StandardError).Contains("json", StringComparison.Ordinal);
        await Assert.That(verifier.CallCount).IsEqualTo(0);
    }

    [Test]
    [Arguments("--mod")]
    [Arguments("--tester")]
    [Arguments("--target")]
    [Arguments("--publish")]
    public async Task VerifyRelease_WhenUnrelatedOrPublishingOptionIsSupplied_RejectsCommand(
        string option)
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var verifier = new CapturingReleaseCandidateVerifier();
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { ReleaseCandidateVerifier = verifier });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            [
                "verify-release",
                "--candidate",
                Path.Combine(fixture.ArtifactsDirectory, "candidate"),
                option,
                "value"
            ]);

        await Assert.That(invocation.ExitCode).IsEqualTo((int)PipelineExitCode.InvalidInput);
        await Assert.That(verifier.CallCount).IsEqualTo(0);
    }
}

internal sealed class CapturingReleaseCandidateVerifier : IReleaseCandidateVerifier
{
    internal const string ContentDigest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    internal int CallCount { get; private set; }

    internal string? CandidateDirectory { get; private set; }

    internal OperationResult<ReleaseReadinessReport>? Result { get; init; }

    public Task<OperationResult<ReleaseReadinessReport>> VerifyAsync(
        string candidateDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        CandidateDirectory = candidateDirectory;
        return Task.FromResult(Result ??
            new OperationResult<ReleaseReadinessReport>(
                CreateReport(ReleaseCandidateState.ReadyForUpload),
                [],
                PipelineExitCode.Success));
    }

    internal static ReleaseReadinessReport CreateReport(ReleaseCandidateState state) =>
        new(
            SchemaVersion: 1,
            StaticId: "Example.Mod",
            Version: "1.2.3",
            ContentDigest,
            PreparedAtUtc: new DateTimeOffset(
                2026,
                8,
                27,
                14,
                3,
                2,
                TimeSpan.Zero),
            state,
            BuildSucceeded: true,
            AutomatedTestsPassed: true,
            PreparedContentVerified: true,
            RelevantSourcesClean: true,
            AutomatedTests: [],
            EvidenceIndex: [],
            BlockingConditions: [],
            IrreversibleInvalidation: null,
            InstalledAtUtc: new DateTimeOffset(
                2026,
                8,
                27,
                19,
                0,
                0,
                TimeSpan.Zero),
            AcceptanceRecordedAtUtc: new DateTimeOffset(
                2026,
                8,
                27,
                20,
                15,
                0,
                TimeSpan.Zero),
            AcceptanceTester: "Release Tester",
            RequiredAcceptancePassed: state == ReleaseCandidateState.ReadyForUpload);
}
