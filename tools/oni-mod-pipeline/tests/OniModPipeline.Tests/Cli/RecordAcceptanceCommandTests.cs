using MaksymShostak.OniModPipeline.Cli;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ReleaseCandidates;

namespace MaksymShostak.OniModPipeline.Tests.Cli;

[TestClass]
public sealed class RecordAcceptanceCommandTests
{
    [TestMethod]
    public async Task RecordAcceptance_WhenHelpIsRequested_DocumentsOnlyInteractiveCandidateInputs()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var recorder = new CapturingAcceptanceRecorder();
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { AcceptanceRecorder = recorder });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            ["record-acceptance", "--help"]);

        Assert.AreEqual(0, invocation.ExitCode);
        Assert.Contains("--candidate", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--tester", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("--mod", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("--format", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("--input", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("--json", invocation.StandardOutput, StringComparison.Ordinal);
        Assert.AreEqual(0, recorder.CallCount);
        Assert.AreEqual(0, fixture.ProcessRunner.Requests.Count);
    }

    [TestMethod]
    public async Task RecordAcceptance_WhenCandidateAndTesterAreExplicit_RecordsExactCandidate()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var recorder = new CapturingAcceptanceRecorder();
        var candidateDirectory = Path.Combine(
            fixture.ArtifactsDirectory,
            "release-candidates",
            "Example.Mod",
            "1.2.3",
            "20260827T140302.1234567Z-0123456789abcdef");
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { AcceptanceRecorder = recorder });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            [
                "record-acceptance",
                "--candidate",
                candidateDirectory,
                "--tester",
                "Maksym Shostak"
            ]);

        Assert.AreEqual(0, invocation.ExitCode);
        Assert.AreEqual(string.Empty, invocation.StandardError);
        Assert.AreEqual(Path.GetFullPath(candidateDirectory), recorder.CandidateDirectory);
        Assert.AreEqual("Maksym Shostak", recorder.Tester);
        Assert.Contains(
            "All checks passed: true",
            invocation.StandardOutput,
            StringComparison.Ordinal);
        Assert.AreEqual(1, recorder.CallCount);
        Assert.AreEqual(0, fixture.ProcessRunner.Requests.Count);
    }

    [TestMethod]
    public async Task RecordAcceptance_WhenTesterIsOmitted_DelegatesInteractiveTesterPrompt()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var recorder = new CapturingAcceptanceRecorder();
        var candidateDirectory = Path.Combine(fixture.ArtifactsDirectory, "candidate");
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { AcceptanceRecorder = recorder });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            ["record-acceptance", "--candidate", candidateDirectory]);

        Assert.AreEqual(0, invocation.ExitCode);
        Assert.IsNull(recorder.Tester);
        Assert.AreEqual(1, recorder.CallCount);
    }

    [TestMethod]
    public async Task RecordAcceptance_WhenRecordedCheckFailed_PreservesReleaseNotReadyExit()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var recorder = new CapturingAcceptanceRecorder
        {
            Result = new OperationResult<AcceptanceRecordingResult>(
                new AcceptanceRecordingResult(
                    "acceptance-test-results.json",
                    "Example.Mod",
                    "1.2.3",
                    CapturingAcceptanceRecorder.ContentDigest,
                    CapturingAcceptanceRecorder.RecordedAt,
                    AllChecksPassed: false),
                [DiagnosticCatalog.RequiredAcceptanceMissing(
                    "Failed acceptance checks: 'first-check'.")],
                PipelineExitCode.ReleaseNotReady)
        };
        var candidateDirectory = Path.Combine(fixture.ArtifactsDirectory, "candidate");
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { AcceptanceRecorder = recorder });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            ["record-acceptance", "--candidate", candidateDirectory]);

        Assert.AreEqual((int)PipelineExitCode.ReleaseNotReady, invocation.ExitCode);
        Assert.Contains(
            DiagnosticIds.RequiredAcceptanceMissing,
            invocation.StandardError,
            StringComparison.Ordinal);
        Assert.AreEqual(1, recorder.CallCount);
    }

    [TestMethod]
    public async Task RecordAcceptance_WhenCandidateIsMissingOrEmpty_RejectsAtParseTime()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var recorder = new CapturingAcceptanceRecorder();
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { AcceptanceRecorder = recorder });

        var missing = await DiagnoseCommandTests.InvokeAsync(
            command,
            ["record-acceptance"]);
        var empty = await DiagnoseCommandTests.InvokeAsync(
            command,
            ["record-acceptance", "--candidate", string.Empty]);

        Assert.AreEqual((int)PipelineExitCode.InvalidInput, missing.ExitCode);
        Assert.Contains("--candidate", missing.StandardError, StringComparison.Ordinal);
        Assert.AreEqual((int)PipelineExitCode.InvalidInput, empty.ExitCode);
        Assert.Contains("nonempty path", empty.StandardError, StringComparison.Ordinal);
        Assert.AreEqual(0, recorder.CallCount);
    }

    [TestMethod]
    public async Task RecordAcceptance_WhenExplicitTesterIsEmpty_RejectsAtParseTime()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var recorder = new CapturingAcceptanceRecorder();
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { AcceptanceRecorder = recorder });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            [
                "record-acceptance",
                "--candidate",
                Path.Combine(fixture.ArtifactsDirectory, "candidate"),
                "--tester",
                "   "
            ]);

        Assert.AreEqual((int)PipelineExitCode.InvalidInput, invocation.ExitCode);
        Assert.Contains("nonempty", invocation.StandardError, StringComparison.Ordinal);
        Assert.AreEqual(0, recorder.CallCount);
    }

    [TestMethod]
    [DataRow("--input")]
    [DataRow("--json")]
    [DataRow("--format")]
    [DataRow("--mod")]
    [DataRow("--target")]
    public async Task RecordAcceptance_WhenUnsupportedImportOrPipelineOptionIsSupplied_RejectsCommand(
        string option)
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var recorder = new CapturingAcceptanceRecorder();
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { AcceptanceRecorder = recorder });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            [
                "record-acceptance",
                "--candidate",
                Path.Combine(fixture.ArtifactsDirectory, "candidate"),
                option,
                "value"
            ]);

        Assert.AreEqual((int)PipelineExitCode.InvalidInput, invocation.ExitCode);
        Assert.AreEqual(0, recorder.CallCount);
    }
}

internal sealed class CapturingAcceptanceRecorder : IAcceptanceRecorder
{
    internal const string ContentDigest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    internal static readonly DateTimeOffset RecordedAt =
        new(2026, 8, 27, 20, 15, 0, TimeSpan.Zero);

    internal int CallCount { get; private set; }

    internal string? CandidateDirectory { get; private set; }

    internal string? Tester { get; private set; }

    internal OperationResult<AcceptanceRecordingResult>? Result { get; init; }

    public Task<OperationResult<AcceptanceRecordingResult>> RecordAsync(
        string candidateDirectory,
        string? tester,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        CandidateDirectory = Path.GetFullPath(candidateDirectory);
        Tester = tester;
        return Task.FromResult(Result ??
            new OperationResult<AcceptanceRecordingResult>(
                new AcceptanceRecordingResult(
                    Path.Combine(
                        CandidateDirectory,
                        "release-evidence",
                        "acceptance-test-results.json"),
                    "Example.Mod",
                    "1.2.3",
                    ContentDigest,
                    RecordedAt,
                    AllChecksPassed: true),
                [],
                PipelineExitCode.Success));
    }
}
