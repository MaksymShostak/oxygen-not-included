using MaksymShostak.OniModPipeline.Cli;
using MaksymShostak.OniModPipeline.ContentIntegrity;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ReleaseCandidates;
using System.Text.Json;

namespace MaksymShostak.OniModPipeline.Tests.Cli;

[TestClass]
public sealed class PrepareReleaseCommandTests
{
    [TestMethod]
    public async Task PrepareRelease_WhenInputsAreClean_PrintsCandidatePathDigestAndState()
    {
        using var fixture = new PipelineCommandFixture(includeTests: true);
        var preparer = new CapturingCandidatePreparer();
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { ReleaseCandidatePreparer = preparer });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            fixture.CreateArguments("prepare-release"));

        Assert.AreEqual(0, invocation.ExitCode);
        Assert.AreEqual(string.Empty, invocation.StandardError);
        Assert.IsNotNull(preparer.Request);
        Assert.AreEqual("Example.Mod", preparer.Request.Metadata.StaticId);
        Assert.AreEqual("1.2.3", preparer.Request.Metadata.Version);
        Assert.IsTrue(preparer.Request.InitialProvenance.IsClean);
        Assert.AreEqual(PipelineCommandFixture.Commit, preparer.Request.InitialProvenance.Commit);
        Assert.AreEqual(fixture.ArtifactsDirectory, preparer.Request.Environment.ArtifactsDirectory);
        Assert.Contains(
            $"Candidate: {preparer.CandidateDirectory}",
            invocation.StandardOutput,
            StringComparison.Ordinal);
        Assert.Contains(
            $"Content digest: {CapturingCandidatePreparer.ContentDigest}",
            invocation.StandardOutput,
            StringComparison.Ordinal);
        Assert.Contains(
            "State: awaiting-acceptance",
            invocation.StandardOutput,
            StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task PrepareRelease_WhenJsonRequested_ReturnsStructuredCandidate()
    {
        using var fixture = new PipelineCommandFixture(includeTests: true);
        var preparer = new CapturingCandidatePreparer();
        var command = CliApplication.CreateRootCommand(
            fixture.Services with { ReleaseCandidatePreparer = preparer });

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            fixture.CreateArguments("prepare-release", "--format", "json"));

        Assert.AreEqual(0, invocation.ExitCode);
        Assert.AreEqual(string.Empty, invocation.StandardError);
        using var document = JsonDocument.Parse(invocation.StandardOutput);
        var value = document.RootElement.GetProperty("value");
        Assert.AreEqual(
            preparer.CandidateDirectory,
            value.GetProperty("candidateDirectory").GetString());
        Assert.AreEqual(
            CapturingCandidatePreparer.ContentDigest,
            value.GetProperty("contentManifest").GetProperty("contentDigest").GetString());
        Assert.AreEqual(
            preparer.CandidateDirectory,
            value.GetProperty("layout").GetProperty("candidateDirectory").GetString());
        Assert.AreEqual("awaiting-acceptance", value.GetProperty("state").GetString());
    }

    [TestMethod]
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

        Assert.AreEqual((int)PipelineExitCode.ReleaseNotReady, invocation.ExitCode);
        Assert.Contains(DiagnosticIds.DirtyReleaseInput, invocation.StandardError);
        Assert.IsNull(preparer.Request);
    }

    [TestMethod]
    [DataRow("--allow-dirty")]
    [DataRow("--skip-tests")]
    [DataRow("--overwrite")]
    [DataRow("--publish")]
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

        Assert.AreEqual((int)PipelineExitCode.InvalidInput, invocation.ExitCode);
        Assert.IsNull(preparer.Request);
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
