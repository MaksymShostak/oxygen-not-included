using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModInstallation;
using MaksymShostak.OniModPipeline.ReleaseCandidates;
using System.Security.Cryptography;
using System.Text;

namespace MaksymShostak.OniModPipeline.Tests.ReleaseCandidates;
public sealed class ReleaseCandidateVerifierTests
{
    [Test]
    public async Task VerifyAsync_WhenAcceptanceEvidenceIsMissing_RemainsAwaitingAcceptance()
    {
        await using var candidate = await CandidateFixture.CreateAwaitingAsync();

        var result = await candidate.VerifyAsync();

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.ReleaseNotReady);
        Assert.NotNull(result.Value);
        await Assert.That(result.Value.State).IsEqualTo(ReleaseCandidateState.AwaitingAcceptance);
        await Assert.That(result.Value.IrreversibleInvalidation).IsNull();
        await Assert.That(result.Value.BlockingConditions.Any(condition =>
            condition.Id == "installation-receipt-missing")).IsTrue();
        await Assert.That(result.Value.BlockingConditions.Any(condition =>
            condition.Id == "acceptance-test-results-missing")).IsTrue();
        await Assert.That(result.Diagnostics.Any(diagnostic =>
            diagnostic.Id == DiagnosticIds.ReleaseNotReady)).IsTrue();
    }

    [Test]
    public async Task VerifyAsync_WhenRequiredAcceptanceFailed_ReturnsAcceptanceFailed()
    {
        await using var candidate = await CandidateFixture.CreateAcceptanceFailedAsync();

        var result = await candidate.VerifyAsync();

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Value!.State).IsEqualTo(ReleaseCandidateState.AcceptanceFailed);
        await Assert.That(result.Value.IrreversibleInvalidation).IsNull();
        await Assert.That(result.Diagnostics.Any(diagnostic =>
            diagnostic.Id == DiagnosticIds.RequiredAcceptanceMissing)).IsTrue();
    }

    [Test]
    public async Task VerifyAsync_WhenEveryRequiredAcceptancePassed_ReturnsReadyForUpload()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();

        var result = await candidate.VerifyAsync();

        await Assert.That(result.IsSuccess).IsTrue().Because(CandidateFixture.Render(result.Diagnostics));
        await Assert.That(result.Value!.State).IsEqualTo(ReleaseCandidateState.ReadyForUpload);
        await Assert.That(result.Value.IrreversibleInvalidation).IsNull();
        await Assert.That(result.Value.BlockingConditions).IsEmpty();
    }

    [Test]
    public async Task VerifyAsync_WhenRequiredTrxIsMissing_ReturnsAutomatedTestFailure()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();
        var readiness = await candidate.ReadJsonAsync<ReleaseReadinessReport>(
            candidate.Layout.ReleaseReadinessReportPath);
        var trxPath = ResolveCandidatePath(
            candidate.Root,
            readiness.AutomatedTests.Single().TrxPath);
        File.Delete(trxPath);

        var result = await candidate.VerifyAsync();

        await AssertVerificationFailed(result, DiagnosticIds.AutomatedTestFailed);
    }

    [Test]
    public async Task VerifyAsync_WhenTrxOutcomeFailed_ReturnsAutomatedTestFailure()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();
        var readiness = await candidate.ReadJsonAsync<ReleaseReadinessReport>(
            candidate.Layout.ReleaseReadinessReportPath);
        var trxPath = ResolveCandidatePath(
            candidate.Root,
            readiness.AutomatedTests.Single().TrxPath);
        await File.WriteAllTextAsync(
            trxPath,
            """
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <ResultSummary outcome="Completed">
                <Counters total="1" executed="1" passed="0" failed="1" error="0" timeout="0" aborted="0" />
              </ResultSummary>
            </TestRun>

            """);

        var result = await candidate.VerifyAsync();

        await AssertVerificationFailed(result, DiagnosticIds.AutomatedTestFailed);
    }

    [Test]
    public async Task VerifyAsync_WhenProvenanceClaimsDirtySources_ReturnsOnip5001()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();
        var provenance = await candidate.ReadJsonAsync<BuildProvenance>(
            candidate.Layout.BuildProvenancePath);
        await candidate.WriteJsonAsync(
            candidate.Layout.BuildProvenancePath,
            provenance with { RelevantPathsClean = false });

        var result = await candidate.VerifyAsync();

        await AssertVerificationFailed(result, DiagnosticIds.DirtyReleaseInput);
    }

    [Test]
    public async Task VerifyAsync_WhenReadyCandidateContentIsTampered_ReturnsOnip5002AndNeverReady()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();
        await File.AppendAllTextAsync(
            candidate.Layout.DescriptionPath,
            "tamper",
            new UTF8Encoding(false));

        var result = await candidate.VerifyAsync();

        await AssertVerificationFailed(result, DiagnosticIds.CandidateManifestMismatch);
    }

    [Test]
    public async Task VerifyAsync_WhenUndeclaredContentFileIsAdded_ReturnsOnip5002()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();
        await File.WriteAllTextAsync(
            Path.Combine(candidate.Layout.WorkshopContentDirectory, "undeclared.txt"),
            "undeclared");

        var result = await candidate.VerifyAsync();

        await AssertVerificationFailed(result, DiagnosticIds.CandidateManifestMismatch);
    }

    [Test]
    public async Task VerifyAsync_WhenAcceptancePlanChanges_ReturnsOnip5003()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();
        await File.AppendAllTextAsync(candidate.Layout.AcceptanceTestPlanPath, " ");

        var result = await candidate.VerifyAsync();

        await AssertVerificationFailed(result, DiagnosticIds.AcceptanceDigestMismatch);
    }

    [Test]
    public async Task VerifyAsync_WhenIndexedInstallationReceiptChanges_ReturnsOnip5003()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();
        var first = await candidate.VerifyAsync();
        await Assert.That(first.IsSuccess).IsTrue().Because(CandidateFixture.Render(first.Diagnostics));
        var receipt = await candidate.ReadJsonAsync<InstallationReceipt>(
            candidate.Layout.InstallationReceiptPath);
        await candidate.WriteJsonAsync(
            candidate.Layout.InstallationReceiptPath,
            receipt with { InstalledAtUtc = receipt.InstalledAtUtc.AddMinutes(1) });

        var result = await candidate.VerifyAsync();

        await AssertVerificationFailed(result, DiagnosticIds.AcceptanceDigestMismatch);
    }

    [Test]
    public async Task VerifyAsync_WhenWorkshopTextUsesLfRepresentation_ReturnsOnip5005()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();
        var original = await File.ReadAllTextAsync(candidate.Layout.DescriptionPath);
        await File.WriteAllTextAsync(
            candidate.Layout.DescriptionPath,
            original.Replace("\r\n", "\n", StringComparison.Ordinal),
            new UTF8Encoding(false));

        var result = await candidate.VerifyAsync();

        await AssertVerificationFailed(result, DiagnosticIds.InvalidUploaderRepresentation);
    }

    [Test]
    public async Task VerifyAsync_WhenObservedContentTamperIsLaterRestored_RemainsVerificationFailed()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();
        var original = await File.ReadAllBytesAsync(candidate.Layout.DescriptionPath);
        var first = await candidate.VerifyAsync();
        await Assert.That(first.IsSuccess).IsTrue().Because(CandidateFixture.Render(first.Diagnostics));
        await File.AppendAllTextAsync(candidate.Layout.DescriptionPath, "tamper");
        var tampered = await candidate.VerifyAsync();
        await AssertVerificationFailed(tampered, DiagnosticIds.CandidateManifestMismatch);
        var irreversible = tampered.Value!.IrreversibleInvalidation;
        Assert.NotNull(irreversible);
        await File.WriteAllBytesAsync(candidate.Layout.DescriptionPath, original);

        var restored = await candidate.VerifyAsync();

        await Assert.That(restored.IsSuccess).IsFalse();
        await Assert.That(restored.Value!.State).IsEqualTo(ReleaseCandidateState.VerificationFailed);
        await Assert.That(restored.Value.IrreversibleInvalidation).IsEqualTo(irreversible);
        await Assert.That(restored.Diagnostics.Any(diagnostic =>
            diagnostic.Id == DiagnosticIds.ReleaseNotReady)).IsTrue();
    }

    [Test]
    public async Task VerifyAsync_WhenReadyCandidateIsVerifiedTwice_DerivedEvidenceIsByteIdentical()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();
        var first = await candidate.VerifyAsync();
        await Assert.That(first.IsSuccess).IsTrue().Because(CandidateFixture.Render(first.Diagnostics));
        var firstEvidence = await ReadDerivedEvidenceAsync(candidate.Layout);

        var second = await candidate.VerifyAsync();

        await Assert.That(second.IsSuccess).IsTrue().Because(CandidateFixture.Render(second.Diagnostics));
        var secondEvidence = await ReadDerivedEvidenceAsync(candidate.Layout);
        foreach (var path in firstEvidence.Keys)
        {
            await Assert.That(secondEvidence[path]).IsEquivalentTo(firstEvidence[path], TUnit.Assertions.Enums.CollectionOrdering.Matching).Because(path);
            await Assert.That(CandidateFixture.Sha256(secondEvidence[path])).IsEqualTo(CandidateFixture.Sha256(firstEvidence[path])).Because(path);
        }
    }

    [Test]
    public async Task VerifyAsync_WhenReady_IndexesEveryFinalEvidenceFileExceptReadinessReport()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();

        var result = await candidate.VerifyAsync();

        await Assert.That(result.IsSuccess).IsTrue().Because(CandidateFixture.Render(result.Diagnostics));
        var report = result.Value!;
        var paths = report.EvidenceIndex.Select(entry => entry.Path).ToArray();
        await Assert.That(paths).IsEquivalentTo(paths.OrderBy(path => path, StringComparer.Ordinal).ToArray(), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(paths).DoesNotContain("release-evidence/release-readiness-report.json");
        var expectedPaths = new[]
        {
            "release-evidence/acceptance-test-plan.json",
            "release-evidence/acceptance-test-results.json",
            "release-evidence/automated-test-results/example-regressions.trx",
            "release-evidence/build-provenance.json",
            "release-evidence/installation-receipt.json",
            "release-evidence/release-content-manifest.json",
            "release-evidence/release-summary.md",
            "release-evidence/uploader-checklist.md"
        };
        await Assert.That(paths).IsEquivalentTo(expectedPaths, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        foreach (var entry in report.EvidenceIndex)
        {
            var absolutePath = ResolveCandidatePath(candidate.Root, entry.Path);
            var bytes = await File.ReadAllBytesAsync(absolutePath);
            await Assert.That(entry.ByteLength).IsEqualTo(bytes.LongLength).Because(entry.Path);
            await Assert.That(entry.Sha256).IsEqualTo(Convert.ToHexStringLower(SHA256.HashData(bytes))).Because(entry.Path);
        }
    }

    [Test]
    public async Task VerifyAsync_WhenIndexedDerivedEvidenceIsEdited_FailsClosed()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();
        var first = await candidate.VerifyAsync();
        await Assert.That(first.IsSuccess).IsTrue().Because(CandidateFixture.Render(first.Diagnostics));
        await File.AppendAllTextAsync(candidate.Layout.ReleaseSummaryPath, "manual edit");

        var result = await candidate.VerifyAsync();

        await AssertVerificationFailed(result, DiagnosticIds.ReleaseNotReady);
    }

    private static async Task AssertVerificationFailed(
        OperationResult<ReleaseReadinessReport> result,
        string diagnosticId)
    {
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.ReleaseNotReady);
        Assert.NotNull(result.Value);
        await Assert.That(result.Value.State).IsEqualTo(ReleaseCandidateState.VerificationFailed);
        Assert.NotNull(result.Value.IrreversibleInvalidation);
        await Assert.That(result.Diagnostics.Any(diagnostic =>
            diagnostic.Id == diagnosticId)).IsTrue().Because(CandidateFixture.Render(result.Diagnostics));
    }

    private static string ResolveCandidatePath(string root, string relativePath) =>
        Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static async Task<Dictionary<string, byte[]>> ReadDerivedEvidenceAsync(
        CandidateLayout layout) =>
        new(StringComparer.Ordinal)
        {
            [layout.ReleaseReadinessReportPath] = await File.ReadAllBytesAsync(
                layout.ReleaseReadinessReportPath),
            [layout.ReleaseSummaryPath] = await File.ReadAllBytesAsync(
                layout.ReleaseSummaryPath),
            [layout.UploaderChecklistPath] = await File.ReadAllBytesAsync(
                layout.UploaderChecklistPath)
        };
}
