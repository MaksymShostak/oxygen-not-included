using MaksymShostak.OniModPipeline.Cli;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ReleaseCandidates;

namespace MaksymShostak.OniModPipeline.Tests.Cli;

[TestClass]
public sealed class VerifyReleaseCommandTests
{
    [TestMethod]
    public async Task VerifyRelease_WhenHelpIsRequested_DocumentsOnlyCandidateAndOutputFormat()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var verifier = new CapturingReleaseCandidateVerifier();
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { ReleaseCandidateVerifier = verifier });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            ["verify-release", "--help"]);

        Assert.AreEqual(0, invocation.ExitCode);
        Assert.Contains("--candidate", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--format", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("human", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("json", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("--mod", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("--tester", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("--target", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("publish", invocation.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.AreEqual(0, verifier.CallCount);
        Assert.AreEqual(0, fixture.ProcessRunner.Requests.Count);
    }

    [TestMethod]
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

        Assert.AreEqual(0, invocation.ExitCode);
        Assert.AreEqual(string.Empty, invocation.StandardError);
        Assert.AreEqual(Path.GetFullPath(candidateDirectory), verifier.CandidateDirectory);
        Assert.Contains("State: ready-for-upload", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Irreversibly invalidated: false", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.AreEqual(1, verifier.CallCount);
        Assert.AreEqual(0, fixture.ProcessRunner.Requests.Count);
    }

    [TestMethod]
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

        Assert.AreEqual(0, invocation.ExitCode);
        Assert.AreEqual(string.Empty, invocation.StandardError);
        Assert.Contains("\"state\": \"ready-for-upload\"", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"exitCode\": 0", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.AreEqual(1, verifier.CallCount);
    }

    [TestMethod]
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

        Assert.AreEqual((int)PipelineExitCode.ReleaseNotReady, invocation.ExitCode);
        Assert.Contains(
            DiagnosticIds.RequiredAcceptanceMissing,
            invocation.StandardError,
            StringComparison.Ordinal);
        Assert.AreEqual(1, verifier.CallCount);
    }

    [TestMethod]
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

        Assert.AreEqual((int)PipelineExitCode.InvalidInput, missing.ExitCode);
        Assert.Contains("--candidate", missing.StandardError, StringComparison.Ordinal);
        Assert.AreEqual((int)PipelineExitCode.InvalidInput, empty.ExitCode);
        Assert.Contains("nonempty path", empty.StandardError, StringComparison.Ordinal);
        Assert.AreEqual((int)PipelineExitCode.InvalidInput, invalidFormat.ExitCode);
        Assert.Contains("human", invalidFormat.StandardError, StringComparison.Ordinal);
        Assert.Contains("json", invalidFormat.StandardError, StringComparison.Ordinal);
        Assert.AreEqual(0, verifier.CallCount);
    }

    [TestMethod]
    [DataRow("--mod")]
    [DataRow("--tester")]
    [DataRow("--target")]
    [DataRow("--publish")]
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

        Assert.AreEqual((int)PipelineExitCode.InvalidInput, invocation.ExitCode);
        Assert.AreEqual(0, verifier.CallCount);
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
