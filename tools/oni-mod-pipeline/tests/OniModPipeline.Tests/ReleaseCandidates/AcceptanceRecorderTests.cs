using MaksymShostak.OniModPipeline.ContentIntegrity;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModInstallation;
using MaksymShostak.OniModPipeline.ReleaseCandidates;
using MaksymShostak.OniModPipeline.Serialization;
using MaksymShostak.OniModPipeline.Tests.ModInstallation;
using System.Text;
using System.Text.Json;

namespace MaksymShostak.OniModPipeline.Tests.ReleaseCandidates;

[TestClass]
public sealed class AcceptanceRecorderTests
{
    [TestMethod]
    public async Task RecordAsync_WhenInputIsNotInteractive_ReturnsOnip5008BeforePromptingOrWriting()
    {
        await using var fixture = await AcceptanceRecorderFixture.CreateInstalledAsync(
            interactive: false);

        var result = await fixture.Recorder.RecordAsync(
            fixture.CandidateDirectory,
            "Tester",
            CancellationToken.None);

        fixture.AssertPreconditionFailure(
            result,
            DiagnosticIds.AcceptanceRequiresInteractiveTerminal);
    }

    [TestMethod]
    public async Task RecordAsync_WhenInstallationReceiptIsMissing_FailsBeforePromptingOrWriting()
    {
        await using var fixture = await AcceptanceRecorderFixture.CreateInstalledAsync();
        File.Delete(fixture.Layout.InstallationReceiptPath);

        var result = await fixture.Recorder.RecordAsync(
            fixture.CandidateDirectory,
            "Tester",
            CancellationToken.None);

        fixture.AssertPreconditionFailure(result, DiagnosticIds.AcceptanceDigestMismatch);
    }

    [TestMethod]
    public async Task RecordAsync_WhenReceiptDigestDiffers_FailsBeforePromptingOrWriting()
    {
        await using var fixture = await AcceptanceRecorderFixture.CreateInstalledAsync();
        var receipt = await fixture.ReadJsonAsync<InstallationReceipt>(
            fixture.Layout.InstallationReceiptPath);
        await fixture.OverwriteJsonAsync(
            fixture.Layout.InstallationReceiptPath,
            receipt with { ContentDigest = AcceptanceRecorderFixture.OtherDigest });

        var result = await fixture.Recorder.RecordAsync(
            fixture.CandidateDirectory,
            "Tester",
            CancellationToken.None);

        fixture.AssertPreconditionFailure(result, DiagnosticIds.AcceptanceDigestMismatch);
    }

    [TestMethod]
    public async Task RecordAsync_WhenCurrentCandidateContentDiffers_FailsBeforePromptingOrWriting()
    {
        await using var fixture = await AcceptanceRecorderFixture.CreateInstalledAsync();
        await File.AppendAllTextAsync(fixture.Layout.DescriptionPath, "tamper");

        var result = await fixture.Recorder.RecordAsync(
            fixture.CandidateDirectory,
            "Tester",
            CancellationToken.None);

        fixture.AssertPreconditionFailure(result, DiagnosticIds.AcceptanceDigestMismatch);
    }

    [TestMethod]
    public async Task RecordAsync_WhenInstalledRuntimeByteDiffers_FailsBeforePromptingOrWriting()
    {
        await using var fixture = await AcceptanceRecorderFixture.CreateInstalledAsync();
        await File.AppendAllTextAsync(
            Path.Combine(fixture.Installation.Destination, "Example.dll"),
            "tamper");

        var result = await fixture.Recorder.RecordAsync(
            fixture.CandidateDirectory,
            "Tester",
            CancellationToken.None);

        fixture.AssertPreconditionFailure(result, DiagnosticIds.AcceptanceDigestMismatch);
    }

    [TestMethod]
    public async Task RecordAsync_WhenAcceptancePlanHashDiffers_FailsBeforePromptingOrWriting()
    {
        await using var fixture = await AcceptanceRecorderFixture.CreateInstalledAsync();
        await File.AppendAllTextAsync(fixture.Layout.AcceptanceTestPlanPath, " ");

        var result = await fixture.Recorder.RecordAsync(
            fixture.CandidateDirectory,
            "Tester",
            CancellationToken.None);

        fixture.AssertPreconditionFailure(result, DiagnosticIds.AcceptanceDigestMismatch);
    }

    [TestMethod]
    public async Task RecordAsync_WhenExplicitTesterIsEmpty_FailsBeforePromptingOrWriting()
    {
        await using var fixture = await AcceptanceRecorderFixture.CreateInstalledAsync();

        var result = await fixture.Recorder.RecordAsync(
            fixture.CandidateDirectory,
            "   ",
            CancellationToken.None);

        fixture.AssertPreconditionFailure(result, DiagnosticIds.RequiredAcceptanceMissing);
        Assert.AreEqual(PipelineExitCode.InvalidInput, result.ExitCode);
    }

    [TestMethod]
    public async Task RecordAsync_WhenResultsAlreadyExist_PreservesTheirExactBytesWithoutPrompting()
    {
        await using var fixture = await AcceptanceRecorderFixture.CreateInstalledAsync();
        var existing = "existing acceptance evidence\n"u8.ToArray();
        await File.WriteAllBytesAsync(
            fixture.Layout.AcceptanceTestResultsPath,
            existing);

        var result = await fixture.Recorder.RecordAsync(
            fixture.CandidateDirectory,
            "Tester",
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PipelineExitCode.ReleaseNotReady, result.ExitCode);
        Assert.AreEqual(DiagnosticIds.ReleaseNotReady, result.Diagnostics[0].Id);
        CollectionAssert.AreEqual(
            existing,
            await File.ReadAllBytesAsync(fixture.Layout.AcceptanceTestResultsPath));
        Assert.AreEqual(0, fixture.Console.InteractionCount);
        Assert.IsEmpty(fixture.Console.Lines);
    }

    [TestMethod]
    public async Task RecordAsync_WhenOneCheckFails_WritesCompleteDigestBoundResultsAndReturnsExitSix()
    {
        await using var fixture = await AcceptanceRecorderFixture.CreateInstalledAsync();
        fixture.Console.Outcomes.Enqueue(AcceptanceOutcome.Passed);
        fixture.Console.Outcomes.Enqueue(AcceptanceOutcome.Failed);
        fixture.Console.Notes.Enqueue("  first note  ");
        fixture.Console.Notes.Enqueue("  failure reproduced  ");

        var result = await fixture.Recorder.RecordAsync(
            fixture.CandidateDirectory,
            "  Maksym Shostak  ",
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PipelineExitCode.ReleaseNotReady, result.ExitCode);
        Assert.AreEqual(DiagnosticIds.RequiredAcceptanceMissing, result.Diagnostics[0].Id);
        Assert.IsNotNull(result.Value);
        Assert.IsFalse(result.Value.AllChecksPassed);
        Assert.IsTrue(File.Exists(fixture.Layout.AcceptanceTestResultsPath));
        var recorded = await fixture.ReadJsonAsync<AcceptanceTestResults>(
            fixture.Layout.AcceptanceTestResultsPath);
        Assert.AreEqual(1, recorded.SchemaVersion);
        Assert.AreEqual("Maksym Shostak", recorded.Tester);
        Assert.AreEqual(AcceptanceRecorderFixture.RecordedAt, recorded.RecordedAtUtc);
        Assert.AreEqual(fixture.InstallationDigest, recorded.ContentDigest);
        Assert.AreEqual(
            fixture.Provenance.AcceptanceTestPlanSha256,
            recorded.AcceptancePlanSha256);
        Assert.HasCount(2, recorded.Checks);
        Assert.AreEqual(AcceptanceOutcome.Passed, recorded.Checks[0].Outcome);
        Assert.AreEqual("first note", recorded.Checks[0].Note);
        Assert.AreEqual(AcceptanceOutcome.Failed, recorded.Checks[1].Outcome);
        Assert.AreEqual("failure reproduced", recorded.Checks[1].Note);
        AssertCheckCopied(fixture.Plan.Checks[0], recorded.Checks[0]);
        AssertCheckCopied(fixture.Plan.Checks[1], recorded.Checks[1]);
        var resultBytes = await File.ReadAllBytesAsync(
            fixture.Layout.AcceptanceTestResultsPath);
        Assert.IsFalse(resultBytes.AsSpan().StartsWith(
            new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.IsFalse(resultBytes.Contains((byte)'\r'));
        var resultJson = Encoding.UTF8.GetString(resultBytes);
        Assert.Contains("\"outcome\": \"passed\"", resultJson, StringComparison.Ordinal);
        Assert.Contains("\"outcome\": \"failed\"", resultJson, StringComparison.Ordinal);
        Assert.Contains(
            $"Candidate static ID: {fixture.Plan.StaticId}",
            fixture.Console.Lines);
        Assert.Contains(
            $"Candidate version: {fixture.Plan.Version}",
            fixture.Console.Lines);
        Assert.Contains(
            $"Candidate content digest: {fixture.Plan.ContentDigest}",
            fixture.Console.Lines);
    }

    [TestMethod]
    public async Task RecordAsync_WhenEveryCheckPasses_WritesOnceAndReturnsAcceptancePassed()
    {
        await using var fixture = await AcceptanceRecorderFixture.CreateInstalledAsync();
        fixture.Console.RequiredValues.Enqueue("  Prompted Tester  ");
        fixture.Console.Outcomes.Enqueue(AcceptanceOutcome.Passed);
        fixture.Console.Outcomes.Enqueue(AcceptanceOutcome.Passed);
        fixture.Console.Notes.Enqueue(null);
        fixture.Console.Notes.Enqueue("   ");

        var result = await fixture.Recorder.RecordAsync(
            fixture.CandidateDirectory,
            tester: null,
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess, AcceptanceRecorderFixture.Render(result.Diagnostics));
        Assert.IsNotNull(result.Value);
        Assert.IsTrue(result.Value.AllChecksPassed);
        Assert.AreEqual(
            fixture.Layout.AcceptanceTestResultsPath,
            result.Value.ResultsPath);
        var recorded = await fixture.ReadJsonAsync<AcceptanceTestResults>(
            fixture.Layout.AcceptanceTestResultsPath);
        Assert.AreEqual("Prompted Tester", recorded.Tester);
        Assert.IsTrue(recorded.Checks.All(check => check.Outcome == AcceptanceOutcome.Passed));
        Assert.IsTrue(recorded.Checks.All(check => check.Note is null));
    }

    [TestMethod]
    public async Task RecordAsync_WhenInvokedAgain_CannotReplaceFailedResults()
    {
        await using var fixture = await AcceptanceRecorderFixture.CreateInstalledAsync();
        fixture.Console.Outcomes.Enqueue(AcceptanceOutcome.Passed);
        fixture.Console.Outcomes.Enqueue(AcceptanceOutcome.Failed);
        fixture.Console.Notes.Enqueue(null);
        fixture.Console.Notes.Enqueue("failure");
        var first = await fixture.Recorder.RecordAsync(
            fixture.CandidateDirectory,
            "Tester",
            CancellationToken.None);
        Assert.AreEqual(PipelineExitCode.ReleaseNotReady, first.ExitCode);
        var originalBytes = await File.ReadAllBytesAsync(
            fixture.Layout.AcceptanceTestResultsPath);
        var originalInteractionCount = fixture.Console.InteractionCount;

        fixture.Console.Outcomes.Enqueue(AcceptanceOutcome.Passed);
        fixture.Console.Outcomes.Enqueue(AcceptanceOutcome.Passed);
        fixture.Console.Notes.Enqueue(null);
        fixture.Console.Notes.Enqueue(null);
        var second = await fixture.Recorder.RecordAsync(
            fixture.CandidateDirectory,
            "Different Tester",
            CancellationToken.None);

        Assert.IsFalse(second.IsSuccess);
        Assert.AreEqual(DiagnosticIds.ReleaseNotReady, second.Diagnostics[0].Id);
        CollectionAssert.AreEqual(
            originalBytes,
            await File.ReadAllBytesAsync(fixture.Layout.AcceptanceTestResultsPath));
        Assert.AreEqual(originalInteractionCount, fixture.Console.InteractionCount);
    }

    [TestMethod]
    public async Task RecordAsync_WhenInstalledBytesChangeDuringPrompting_DoesNotWriteResults()
    {
        await using var fixture = await AcceptanceRecorderFixture.CreateInstalledAsync();
        fixture.Console.Outcomes.Enqueue(AcceptanceOutcome.Passed);
        fixture.Console.Outcomes.Enqueue(AcceptanceOutcome.Passed);
        fixture.Console.Notes.Enqueue(null);
        fixture.Console.Notes.Enqueue(null);
        fixture.Console.BeforeFirstOutcome = () => File.AppendAllText(
            Path.Combine(fixture.Installation.Destination, "Example.dll"),
            "changed during acceptance");

        var result = await fixture.Recorder.RecordAsync(
            fixture.CandidateDirectory,
            "Tester",
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(DiagnosticIds.AcceptanceDigestMismatch, result.Diagnostics[0].Id);
        Assert.IsFalse(File.Exists(fixture.Layout.AcceptanceTestResultsPath));
    }

    private static void AssertCheckCopied(
        AcceptanceTestPlanCheck expected,
        AcceptanceCheckResult actual)
    {
        Assert.AreEqual(expected.Id, actual.Id);
        Assert.AreEqual(expected.Title, actual.Title);
        Assert.AreEqual(expected.Setup, actual.Setup);
        Assert.AreEqual(expected.Action, actual.Action);
        Assert.AreEqual(expected.Expected, actual.Expected);
    }
}

internal sealed class AcceptanceRecorderFixture : IAsyncDisposable
{
    internal const string OtherDigest =
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";

    internal static readonly DateTimeOffset RecordedAt =
        new(2026, 8, 27, 20, 15, 0, TimeSpan.Zero);

    private AcceptanceRecorderFixture(
        InstallerFixture installation,
        FakeAcceptanceConsole console)
    {
        Installation = installation;
        Console = console;
        CandidateDirectory = installation.CandidateDirectory;
        Layout = CandidateLayout.FromCandidateDirectory(CandidateDirectory);
        Recorder = new AcceptanceRecorder(
            new ContentHasher(),
            Console,
            new FixedTimeProvider(RecordedAt));
    }

    internal InstallerFixture Installation { get; }

    internal FakeAcceptanceConsole Console { get; }

    internal string CandidateDirectory { get; }

    internal CandidateLayout Layout { get; }

    internal AcceptanceRecorder Recorder { get; }

    internal AcceptanceTestPlan Plan { get; private set; } = null!;

    internal BuildProvenance Provenance { get; private set; } = null!;

    internal string InstallationDigest { get; private set; } = null!;

    internal static async Task<AcceptanceRecorderFixture> CreateInstalledAsync(
        bool interactive = true)
    {
        var installation = await InstallerFixture.CreateAsync();
        var installed = await installation.Installer.InstallCandidateAsync(
            installation.CandidateDirectory,
            InstallTarget.Dev,
            installation.Environment,
            CancellationToken.None);
        Assert.IsTrue(installed.IsSuccess, Render(installed.Diagnostics));
        var fixture = new AcceptanceRecorderFixture(
            installation,
            new FakeAcceptanceConsole(interactive));
        fixture.Plan = await fixture.ReadJsonAsync<AcceptanceTestPlan>(
            fixture.Layout.AcceptanceTestPlanPath);
        fixture.Provenance = await fixture.ReadJsonAsync<BuildProvenance>(
            fixture.Layout.BuildProvenancePath);
        fixture.InstallationDigest = installed.Value!.ContentDigest;
        return fixture;
    }

    internal async Task<T> ReadJsonAsync<T>(string path) =>
        JsonSerializer.Deserialize<T>(
            await File.ReadAllBytesAsync(path),
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!;

    internal async Task OverwriteJsonAsync<T>(string path, T value) =>
        await new Utf8ArtifactWriter().WriteJsonAtomicallyAsync(
            path,
            value,
            CancellationToken.None);

    internal void AssertPreconditionFailure<T>(
        OperationResult<T> result,
        string diagnosticId)
    {
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(diagnosticId, result.Diagnostics[0].Id);
        Assert.IsFalse(File.Exists(Layout.AcceptanceTestResultsPath));
        Assert.AreEqual(0, Console.InteractionCount);
        Assert.IsEmpty(Console.Lines);
    }

    internal static string Render(IReadOnlyList<Diagnostic> diagnostics) =>
        string.Join(
            Environment.NewLine,
            diagnostics.Select(diagnostic =>
                $"{diagnostic.Id}: {diagnostic.Summary} {diagnostic.Evidence}"));

    public async ValueTask DisposeAsync() =>
        await Installation.DisposeAsync();
}

internal sealed class FakeAcceptanceConsole(bool interactive) : IAcceptanceConsole
{
    private bool firstOutcome = true;

    internal Queue<string> RequiredValues { get; } = [];

    internal Queue<AcceptanceOutcome> Outcomes { get; } = [];

    internal Queue<string?> Notes { get; } = [];

    internal List<string> Lines { get; } = [];

    internal Action? BeforeFirstOutcome { get; set; }

    internal int InteractionCount { get; private set; }

    public bool IsInteractive => interactive;

    public void WriteLine(string value) => Lines.Add(value);

    public string ReadRequired(string prompt)
    {
        InteractionCount++;
        return RequiredValues.Dequeue();
    }

    public AcceptanceOutcome ReadOutcome(string prompt)
    {
        InteractionCount++;
        if (firstOutcome)
        {
            firstOutcome = false;
            BeforeFirstOutcome?.Invoke();
        }

        return Outcomes.Dequeue();
    }

    public string? ReadOptional(string prompt)
    {
        InteractionCount++;
        return Notes.Dequeue();
    }
}
