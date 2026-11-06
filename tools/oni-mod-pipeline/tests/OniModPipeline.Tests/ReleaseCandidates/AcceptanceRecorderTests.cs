using MaksymShostak.OniModPipeline.ContentIntegrity;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModInstallation;
using MaksymShostak.OniModPipeline.ReleaseCandidates;
using MaksymShostak.OniModPipeline.Serialization;
using MaksymShostak.OniModPipeline.Tests.ModInstallation;
using System.Text;
using System.Text.Json;

namespace MaksymShostak.OniModPipeline.Tests.ReleaseCandidates;
public sealed class AcceptanceRecorderTests
{
    [Test]
    public async Task RecordAsync_WhenInputIsNotInteractive_ReturnsOnip5008BeforePromptingOrWriting()
    {
        await using var fixture = await AcceptanceRecorderFixture.CreateInstalledAsync(
            interactive: false);

        var result = await fixture.Recorder.RecordAsync(
            fixture.CandidateDirectory,
            "Tester",
            CancellationToken.None);

        await fixture.AssertPreconditionFailure(
            result,
            DiagnosticIds.AcceptanceRequiresInteractiveTerminal);
    }

    [Test]
    public async Task RecordAsync_WhenInstallationReceiptIsMissing_FailsBeforePromptingOrWriting()
    {
        await using var fixture = await AcceptanceRecorderFixture.CreateInstalledAsync();
        File.Delete(fixture.Layout.InstallationReceiptPath);

        var result = await fixture.Recorder.RecordAsync(
            fixture.CandidateDirectory,
            "Tester",
            CancellationToken.None);

        await fixture.AssertPreconditionFailure(result, DiagnosticIds.AcceptanceDigestMismatch);
    }

    [Test]
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

        await fixture.AssertPreconditionFailure(result, DiagnosticIds.AcceptanceDigestMismatch);
    }

    [Test]
    public async Task RecordAsync_WhenCurrentCandidateContentDiffers_FailsBeforePromptingOrWriting()
    {
        await using var fixture = await AcceptanceRecorderFixture.CreateInstalledAsync();
        await File.AppendAllTextAsync(fixture.Layout.DescriptionPath, "tamper");

        var result = await fixture.Recorder.RecordAsync(
            fixture.CandidateDirectory,
            "Tester",
            CancellationToken.None);

        await fixture.AssertPreconditionFailure(result, DiagnosticIds.AcceptanceDigestMismatch);
    }

    [Test]
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

        await fixture.AssertPreconditionFailure(result, DiagnosticIds.AcceptanceDigestMismatch);
    }

    [Test]
    public async Task RecordAsync_WhenAcceptancePlanHashDiffers_FailsBeforePromptingOrWriting()
    {
        await using var fixture = await AcceptanceRecorderFixture.CreateInstalledAsync();
        await File.AppendAllTextAsync(fixture.Layout.AcceptanceTestPlanPath, " ");

        var result = await fixture.Recorder.RecordAsync(
            fixture.CandidateDirectory,
            "Tester",
            CancellationToken.None);

        await fixture.AssertPreconditionFailure(result, DiagnosticIds.AcceptanceDigestMismatch);
    }

    [Test]
    public async Task RecordAsync_WhenExplicitTesterIsEmpty_FailsBeforePromptingOrWriting()
    {
        await using var fixture = await AcceptanceRecorderFixture.CreateInstalledAsync();

        var result = await fixture.Recorder.RecordAsync(
            fixture.CandidateDirectory,
            "   ",
            CancellationToken.None);

        await fixture.AssertPreconditionFailure(result, DiagnosticIds.RequiredAcceptanceMissing);
        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.InvalidInput);
    }

    [Test]
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

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.ReleaseNotReady);
        await Assert.That(result.Diagnostics[0].Id).IsEqualTo(DiagnosticIds.ReleaseNotReady);
        await Assert.That(await File.ReadAllBytesAsync(fixture.Layout.AcceptanceTestResultsPath)).IsEquivalentTo(existing, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(fixture.Console.InteractionCount).IsEqualTo(0);
        await Assert.That(fixture.Console.Lines).IsEmpty();
    }

    [Test]
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

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.ReleaseNotReady);
        await Assert.That(result.Diagnostics[0].Id).IsEqualTo(DiagnosticIds.RequiredAcceptanceMissing);
        Assert.NotNull(result.Value);
        await Assert.That(result.Value.AllChecksPassed).IsFalse();
        await Assert.That(File.Exists(fixture.Layout.AcceptanceTestResultsPath)).IsTrue();
        var recorded = await fixture.ReadJsonAsync<AcceptanceTestResults>(
            fixture.Layout.AcceptanceTestResultsPath);
        await Assert.That(recorded.SchemaVersion).IsEqualTo(1);
        await Assert.That(recorded.Tester).IsEqualTo("Maksym Shostak");
        await Assert.That(recorded.RecordedAtUtc).IsEqualTo(AcceptanceRecorderFixture.RecordedAt);
        await Assert.That(recorded.ContentDigest).IsEqualTo(fixture.InstallationDigest);
        await Assert.That(recorded.AcceptancePlanSha256).IsEqualTo(fixture.Provenance.AcceptanceTestPlanSha256);
        await Assert.That(recorded.Checks).Count().IsEqualTo(2);
        await Assert.That(recorded.Checks[0].Outcome).IsEqualTo(AcceptanceOutcome.Passed);
        await Assert.That(recorded.Checks[0].Note).IsEqualTo("first note");
        await Assert.That(recorded.Checks[1].Outcome).IsEqualTo(AcceptanceOutcome.Failed);
        await Assert.That(recorded.Checks[1].Note).IsEqualTo("failure reproduced");
        await AssertCheckCopied(fixture.Plan.Checks[0], recorded.Checks[0]);
        await AssertCheckCopied(fixture.Plan.Checks[1], recorded.Checks[1]);
        var resultBytes = await File.ReadAllBytesAsync(
            fixture.Layout.AcceptanceTestResultsPath);
        await Assert.That(resultBytes.AsSpan().StartsWith(
            new byte[] { 0xEF, 0xBB, 0xBF })).IsFalse();
        await Assert.That(resultBytes.Contains((byte)'\r')).IsFalse();
        var resultJson = Encoding.UTF8.GetString(resultBytes);
        await Assert.That(resultJson).Contains("\"outcome\": \"passed\"", StringComparison.Ordinal);
        await Assert.That(resultJson).Contains("\"outcome\": \"failed\"", StringComparison.Ordinal);
        await Assert.That(fixture.Console.Lines).Contains($"Candidate static ID: {fixture.Plan.StaticId}");
        await Assert.That(fixture.Console.Lines).Contains($"Candidate version: {fixture.Plan.Version}");
        await Assert.That(fixture.Console.Lines).Contains($"Candidate content digest: {fixture.Plan.ContentDigest}");
    }

    [Test]
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

        await Assert.That(result.IsSuccess).IsTrue().Because(AcceptanceRecorderFixture.Render(result.Diagnostics));
        Assert.NotNull(result.Value);
        await Assert.That(result.Value.AllChecksPassed).IsTrue();
        await Assert.That(result.Value.ResultsPath).IsEqualTo(fixture.Layout.AcceptanceTestResultsPath);
        var recorded = await fixture.ReadJsonAsync<AcceptanceTestResults>(
            fixture.Layout.AcceptanceTestResultsPath);
        await Assert.That(recorded.Tester).IsEqualTo("Prompted Tester");
        await Assert.That(recorded.Checks.All(check => check.Outcome == AcceptanceOutcome.Passed)).IsTrue();
        await Assert.That(recorded.Checks.All(check => check.Note is null)).IsTrue();
    }

    [Test]
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
        await Assert.That(first.ExitCode).IsEqualTo(PipelineExitCode.ReleaseNotReady);
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

        await Assert.That(second.IsSuccess).IsFalse();
        await Assert.That(second.Diagnostics[0].Id).IsEqualTo(DiagnosticIds.ReleaseNotReady);
        await Assert.That(await File.ReadAllBytesAsync(fixture.Layout.AcceptanceTestResultsPath)).IsEquivalentTo(originalBytes, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(fixture.Console.InteractionCount).IsEqualTo(originalInteractionCount);
    }

    [Test]
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

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Diagnostics[0].Id).IsEqualTo(DiagnosticIds.AcceptanceDigestMismatch);
        await Assert.That(File.Exists(fixture.Layout.AcceptanceTestResultsPath)).IsFalse();
    }

    private static async Task AssertCheckCopied(
        AcceptanceTestPlanCheck expected,
        AcceptanceCheckResult actual)
    {
        await Assert.That(actual.Id).IsEqualTo(expected.Id);
        await Assert.That(actual.Title).IsEqualTo(expected.Title);
        await Assert.That(actual.Setup).IsEqualTo(expected.Setup);
        await Assert.That(actual.Action).IsEqualTo(expected.Action);
        await Assert.That(actual.Expected).IsEqualTo(expected.Expected);
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
        await Assert.That(installed.IsSuccess).IsTrue().Because(Render(installed.Diagnostics));
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

    internal async Task AssertPreconditionFailure<T>(
        OperationResult<T> result,
        string diagnosticId)
    {
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Diagnostics[0].Id).IsEqualTo(diagnosticId);
        await Assert.That(File.Exists(Layout.AcceptanceTestResultsPath)).IsFalse();
        await Assert.That(Console.InteractionCount).IsEqualTo(0);
        await Assert.That(Console.Lines).IsEmpty();
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
