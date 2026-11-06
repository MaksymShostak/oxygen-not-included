using MaksymShostak.OniModPipeline.Cli;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ReleaseCandidates;

namespace MaksymShostak.OniModPipeline.Tests.Cli;
public sealed class RecordAcceptanceCommandTests
{
    [Test]
    public async Task RecordAcceptance_WhenHelpIsRequested_DocumentsOnlyInteractiveCandidateInputs()
    {
        using var fixture = new PipelineCommandFixture(includeTests: false);
        var recorder = new CapturingAcceptanceRecorder();
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { AcceptanceRecorder = recorder });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            ["record-acceptance", "--help"]);

        await Assert.That(invocation.ExitCode).IsEqualTo(0);
        await Assert.That(invocation.StandardOutput).Contains("--candidate", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).Contains("--tester", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).DoesNotContain("--mod", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).DoesNotContain("--format", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).DoesNotContain("--input", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).DoesNotContain("--json", StringComparison.Ordinal);
        await Assert.That(recorder.CallCount).IsEqualTo(0);
        await Assert.That(fixture.ProcessRunner.Requests.Count).IsEqualTo(0);
    }

    [Test]
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

        await Assert.That(invocation.ExitCode).IsEqualTo(0);
        await Assert.That(invocation.StandardError).IsEqualTo(string.Empty);
        await Assert.That(recorder.CandidateDirectory).IsEqualTo(Path.GetFullPath(candidateDirectory));
        await Assert.That(recorder.Tester).IsEqualTo("Maksym Shostak");
        await Assert.That(invocation.StandardOutput).Contains("All checks passed: true", StringComparison.Ordinal);
        await Assert.That(recorder.CallCount).IsEqualTo(1);
        await Assert.That(fixture.ProcessRunner.Requests.Count).IsEqualTo(0);
    }

    [Test]
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

        await Assert.That(invocation.ExitCode).IsEqualTo(0);
        await Assert.That(recorder.Tester).IsNull();
        await Assert.That(recorder.CallCount).IsEqualTo(1);
    }

    [Test]
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

        await Assert.That(invocation.ExitCode).IsEqualTo((int)PipelineExitCode.ReleaseNotReady);
        await Assert.That(invocation.StandardError).Contains(DiagnosticIds.RequiredAcceptanceMissing, StringComparison.Ordinal);
        await Assert.That(recorder.CallCount).IsEqualTo(1);
    }

    [Test]
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

        await Assert.That(missing.ExitCode).IsEqualTo((int)PipelineExitCode.InvalidInput);
        await Assert.That(missing.StandardError).Contains("--candidate", StringComparison.Ordinal);
        await Assert.That(empty.ExitCode).IsEqualTo((int)PipelineExitCode.InvalidInput);
        await Assert.That(empty.StandardError).Contains("nonempty path", StringComparison.Ordinal);
        await Assert.That(recorder.CallCount).IsEqualTo(0);
    }

    [Test]
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

        await Assert.That(invocation.ExitCode).IsEqualTo((int)PipelineExitCode.InvalidInput);
        await Assert.That(invocation.StandardError).Contains("nonempty", StringComparison.Ordinal);
        await Assert.That(recorder.CallCount).IsEqualTo(0);
    }

    [Test]
    [Arguments("--input")]
    [Arguments("--json")]
    [Arguments("--format")]
    [Arguments("--mod")]
    [Arguments("--target")]
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

        await Assert.That(invocation.ExitCode).IsEqualTo((int)PipelineExitCode.InvalidInput);
        await Assert.That(recorder.CallCount).IsEqualTo(0);
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
