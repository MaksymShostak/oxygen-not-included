using MaksymShostak.OniModPipeline.Cli;
using MaksymShostak.OniModPipeline.ContentIntegrity;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ReleaseCandidates;
using System.Text.Json;

namespace MaksymShostak.OniModPipeline.Tests.Cli;
public sealed class PrepareReleaseCommandTests
{
    [Test]
    public async Task PrepareRelease_WhenInputsAreClean_PrintsCandidatePathDigestAndState()
    {
        using var fixture = new PipelineCommandFixture(includeTests: true);
        var preparer = new CapturingCandidatePreparer();
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { ReleaseCandidatePreparer = preparer });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            fixture.CreateArguments("prepare-release"));

        await Assert.That(invocation.ExitCode).IsEqualTo(0);
        await Assert.That(invocation.StandardError).IsEqualTo(string.Empty);
        Assert.NotNull(preparer.Request);
        await Assert.That(preparer.Request.Metadata.StaticId).IsEqualTo("Example.Mod");
        await Assert.That(preparer.Request.Metadata.Version).IsEqualTo("1.2.3");
        await Assert.That(preparer.Request.InitialProvenance.IsClean).IsTrue();
        await Assert.That(preparer.Request.InitialProvenance.Commit).IsEqualTo(PipelineCommandFixture.Commit);
        await Assert.That(preparer.Request.Environment.ArtifactsDirectory).IsEqualTo(fixture.ArtifactsDirectory);
        await Assert.That(invocation.StandardOutput).Contains($"Candidate: {preparer.CandidateDirectory}", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).Contains($"Content digest: {CapturingCandidatePreparer.ContentDigest}", StringComparison.Ordinal);
        await Assert.That(invocation.StandardOutput).Contains("State: awaiting-acceptance", StringComparison.Ordinal);
    }

    [Test]
    public async Task PrepareRelease_WhenJsonRequested_ReturnsStructuredCandidate()
    {
        using var fixture = new PipelineCommandFixture(includeTests: true);
        var preparer = new CapturingCandidatePreparer();
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { ReleaseCandidatePreparer = preparer });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            fixture.CreateArguments("prepare-release", "--format", "json"));

        await Assert.That(invocation.ExitCode).IsEqualTo(0);
        await Assert.That(invocation.StandardError).IsEqualTo(string.Empty);
        using var document = JsonDocument.Parse(invocation.StandardOutput);
        var value = document.RootElement.GetProperty("value");
        await Assert.That(value.GetProperty("candidateDirectory").GetString()).IsEqualTo(preparer.CandidateDirectory);
        await Assert.That(value.GetProperty("contentManifest").GetProperty("contentDigest").GetString()).IsEqualTo(CapturingCandidatePreparer.ContentDigest);
        await Assert.That(value.GetProperty("layout").GetProperty("candidateDirectory").GetString()).IsEqualTo(preparer.CandidateDirectory);
        await Assert.That(value.GetProperty("state").GetString()).IsEqualTo("awaiting-acceptance");
    }

    [Test]
    public async Task PrepareRelease_WhenContributingInputIsDirty_FailsBeforePreparer()
    {
        using var fixture = new PipelineCommandFixture(includeTests: true);
        fixture.ProcessRunner.GitStatusOutput =
            " M mods/example/mod.yaml\0";
        var preparer = new CapturingCandidatePreparer();
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { ReleaseCandidatePreparer = preparer });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            fixture.CreateArguments("prepare-release"));

        await Assert.That(invocation.ExitCode).IsEqualTo((int)PipelineExitCode.ReleaseNotReady);
        await Assert.That(invocation.StandardError).Contains(DiagnosticIds.DirtyReleaseInput);
        await Assert.That(preparer.Request).IsNull();
    }

    [Test]
    [Arguments("--allow-dirty")]
    [Arguments("--skip-tests")]
    [Arguments("--overwrite")]
    [Arguments("--publish")]
    public async Task PrepareRelease_WhenBypassOrPublishOptionIsSupplied_RejectsCommand(
        string unsupportedOption)
    {
        using var fixture = new PipelineCommandFixture(includeTests: true);
        var preparer = new CapturingCandidatePreparer();
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { ReleaseCandidatePreparer = preparer });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            fixture.CreateArguments("prepare-release", unsupportedOption));

        await Assert.That(invocation.ExitCode).IsEqualTo((int)PipelineExitCode.InvalidInput);
        await Assert.That(preparer.Request).IsNull();
    }
}

internal sealed class CapturingCandidatePreparer : IReleaseCandidatePreparer
{
    internal const string ContentDigest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    internal ReleasePreparationRequest? Request { get; private set; }

    internal string? CandidateDirectory { get; private set; }

    public Task<OperationResult<PreparedReleaseCandidate>> PrepareAsync(
        ReleasePreparationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Request = request;
        var layout = CandidateLayout.Create(
            request.Environment.ArtifactsDirectory,
            request.Metadata.StaticId,
            request.Metadata.Version,
            "20260827T140302.1234567Z-0123456789abcdef");
        CandidateDirectory = layout.CandidateDirectory;
        var candidate = new PreparedReleaseCandidate(
            layout.CandidateDirectory,
            layout,
            new ReleaseContentManifest(1, [], ContentDigest),
            null!,
            ReleaseCandidateState.AwaitingAcceptance);
        return Task.FromResult(new OperationResult<PreparedReleaseCandidate>(
            candidate,
            [],
            PipelineExitCode.Success));
    }
}
