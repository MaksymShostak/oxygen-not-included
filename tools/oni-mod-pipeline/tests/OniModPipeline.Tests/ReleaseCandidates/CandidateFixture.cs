using MaksymShostak.OniModPipeline.ContentIntegrity;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModInstallation;
using MaksymShostak.OniModPipeline.ReleaseCandidates;
using MaksymShostak.OniModPipeline.Serialization;
using System.Security.Cryptography;
using System.Text.Json;

namespace MaksymShostak.OniModPipeline.Tests.ReleaseCandidates;

internal sealed class CandidateFixture : IAsyncDisposable
{
    private readonly PreparationFixture preparation;

    private CandidateFixture(PreparationFixture preparation)
    {
        this.preparation = preparation;
        Layout = preparation.Layout;
        Root = Layout.CandidateDirectory;
        Verifier = ReleaseCandidateVerifier.CreateDefault();
    }

    internal string Root { get; }

    internal CandidateLayout Layout { get; }

    internal ReleaseCandidateVerifier Verifier { get; }

    internal string? InstalledDirectory { get; private set; }

    internal static async Task<CandidateFixture> CreateAwaitingAsync()
    {
        var preparation = new PreparationFixture();
        var prepared = await preparation.Preparer.PrepareAsync(
            preparation.Request,
            CancellationToken.None);
        Assert.IsTrue(prepared.IsSuccess, Render(prepared.Diagnostics));
        return new CandidateFixture(preparation);
    }

    internal static async Task<CandidateFixture> CreateReadyAsync()
    {
        var fixture = await CreateAwaitingAsync();
        await fixture.InstallAsync();
        var recorded = await fixture.RecordAcceptanceAsync(
            failedCheckId: null);
        Assert.IsTrue(recorded.IsSuccess, Render(recorded.Diagnostics));
        return fixture;
    }

    internal static async Task<CandidateFixture> CreateAcceptanceFailedAsync()
    {
        var fixture = await CreateAwaitingAsync();
        await fixture.InstallAsync();
        var plan = await fixture.ReadJsonAsync<AcceptanceTestPlan>(
            fixture.Layout.AcceptanceTestPlanPath);
        var recorded = await fixture.RecordAcceptanceAsync(plan.Checks[0].Id);
        Assert.AreEqual(PipelineExitCode.ReleaseNotReady, recorded.ExitCode);
        Assert.IsTrue(File.Exists(fixture.Layout.AcceptanceTestResultsPath));
        return fixture;
    }

    internal async Task<OperationResult<ReleaseReadinessReport>> VerifyAsync() =>
        await Verifier.VerifyAsync(Root, CancellationToken.None);

    internal async Task<T> ReadJsonAsync<T>(string path) =>
        JsonSerializer.Deserialize<T>(
            await File.ReadAllBytesAsync(path),
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!;

    internal async Task WriteJsonAsync<T>(string path, T value) =>
        await new Utf8ArtifactWriter().WriteJsonAtomicallyAsync(
            path,
            value,
            CancellationToken.None);

    internal static string Sha256(byte[] value) =>
        Convert.ToHexStringLower(SHA256.HashData(value));

    internal static string Render(IReadOnlyList<Diagnostic> diagnostics) =>
        string.Join(
            Environment.NewLine,
            diagnostics.Select(diagnostic =>
                $"{diagnostic.Id}: {diagnostic.Summary} {diagnostic.Evidence}"));

    public ValueTask DisposeAsync()
    {
        preparation.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task InstallAsync()
    {
        var installed = await ModInstaller.CreateDefault().InstallCandidateAsync(
            Root,
            InstallTarget.Dev,
            preparation.Environment,
            CancellationToken.None);
        Assert.IsTrue(installed.IsSuccess, Render(installed.Diagnostics));
        InstalledDirectory = installed.Value!.AbsoluteTargetPath;
    }

    private async Task<OperationResult<AcceptanceRecordingResult>> RecordAcceptanceAsync(
        string? failedCheckId)
    {
        var plan = await ReadJsonAsync<AcceptanceTestPlan>(
            Layout.AcceptanceTestPlanPath);
        var console = new FakeAcceptanceConsole(interactive: true);
        foreach (var check in plan.Checks)
        {
            console.Outcomes.Enqueue(
                string.Equals(check.Id, failedCheckId, StringComparison.Ordinal)
                    ? AcceptanceOutcome.Failed
                    : AcceptanceOutcome.Passed);
            console.Notes.Enqueue(
                string.Equals(check.Id, failedCheckId, StringComparison.Ordinal)
                    ? "Observed acceptance failure"
                    : null);
        }

        var recorder = new AcceptanceRecorder(
            new ContentHasher(),
            console,
            new FixedTimeProvider(AcceptanceRecorderFixture.RecordedAt));
        return await recorder.RecordAsync(
            Root,
            "Release Tester",
            CancellationToken.None);
    }
}
