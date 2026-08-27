using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModInstallation;
using MaksymShostak.OniModPipeline.ReleaseCandidates;
using System.Security.Cryptography;
using System.Text;

namespace MaksymShostak.OniModPipeline.Tests.ReleaseCandidates;

[TestClass]
public sealed class ReleaseCandidateVerifierTests
{
    [TestMethod]
    public async Task VerifyAsync_WhenAcceptanceEvidenceIsMissing_RemainsAwaitingAcceptance()
    {
        await using var candidate = await CandidateFixture.CreateAwaitingAsync();

        var result = await candidate.VerifyAsync();

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PipelineExitCode.ReleaseNotReady, result.ExitCode);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual(ReleaseCandidateState.AwaitingAcceptance, result.Value.State);
        Assert.IsNull(result.Value.IrreversibleInvalidation);
        Assert.IsTrue(result.Value.BlockingConditions.Any(condition =>
            condition.Id == "installation-receipt-missing"));
        Assert.IsTrue(result.Value.BlockingConditions.Any(condition =>
            condition.Id == "acceptance-test-results-missing"));
        Assert.IsTrue(result.Diagnostics.Any(diagnostic =>
            diagnostic.Id == DiagnosticIds.ReleaseNotReady));
    }

    [TestMethod]
    public async Task VerifyAsync_WhenRequiredAcceptanceFailed_ReturnsAcceptanceFailed()
    {
        await using var candidate = await CandidateFixture.CreateAcceptanceFailedAsync();

        var result = await candidate.VerifyAsync();

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ReleaseCandidateState.AcceptanceFailed, result.Value!.State);
        Assert.IsNull(result.Value.IrreversibleInvalidation);
        Assert.IsTrue(result.Diagnostics.Any(diagnostic =>
            diagnostic.Id == DiagnosticIds.RequiredAcceptanceMissing));
    }

    [TestMethod]
    public async Task VerifyAsync_WhenEveryRequiredAcceptancePassed_ReturnsReadyForUpload()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();

        var result = await candidate.VerifyAsync();

        Assert.IsTrue(result.IsSuccess, CandidateFixture.Render(result.Diagnostics));
        Assert.AreEqual(ReleaseCandidateState.ReadyForUpload, result.Value!.State);
        Assert.IsNull(result.Value.IrreversibleInvalidation);
        Assert.IsEmpty(result.Value.BlockingConditions);
    }

    [TestMethod]
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

        AssertVerificationFailed(result, DiagnosticIds.AutomatedTestFailed);
    }

    [TestMethod]
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

        AssertVerificationFailed(result, DiagnosticIds.AutomatedTestFailed);
    }

    [TestMethod]
    public async Task VerifyAsync_WhenProvenanceClaimsDirtySources_ReturnsOnip5001()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();
        var provenance = await candidate.ReadJsonAsync<BuildProvenance>(
            candidate.Layout.BuildProvenancePath);
        await candidate.WriteJsonAsync(
            candidate.Layout.BuildProvenancePath,
            provenance with { RelevantPathsClean = false });

        var result = await candidate.VerifyAsync();

        AssertVerificationFailed(result, DiagnosticIds.DirtyReleaseInput);
    }

    [TestMethod]
    public async Task VerifyAsync_WhenReadyCandidateContentIsTampered_ReturnsOnip5002AndNeverReady()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();
        await File.AppendAllTextAsync(
            candidate.Layout.DescriptionPath,
            "tamper",
            new UTF8Encoding(false));

        var result = await candidate.VerifyAsync();

        AssertVerificationFailed(result, DiagnosticIds.CandidateManifestMismatch);
    }

    [TestMethod]
    public async Task VerifyAsync_WhenUndeclaredContentFileIsAdded_ReturnsOnip5002()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();
        await File.WriteAllTextAsync(
            Path.Combine(candidate.Layout.WorkshopContentDirectory, "undeclared.txt"),
            "undeclared");

        var result = await candidate.VerifyAsync();

        AssertVerificationFailed(result, DiagnosticIds.CandidateManifestMismatch);
    }

    [TestMethod]
    public async Task VerifyAsync_WhenAcceptancePlanChanges_ReturnsOnip5003()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();
        await File.AppendAllTextAsync(candidate.Layout.AcceptanceTestPlanPath, " ");

        var result = await candidate.VerifyAsync();

        AssertVerificationFailed(result, DiagnosticIds.AcceptanceDigestMismatch);
    }

    [TestMethod]
    public async Task VerifyAsync_WhenIndexedInstallationReceiptChanges_ReturnsOnip5003()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();
        var first = await candidate.VerifyAsync();
        Assert.IsTrue(first.IsSuccess, CandidateFixture.Render(first.Diagnostics));
        var receipt = await candidate.ReadJsonAsync<InstallationReceipt>(
            candidate.Layout.InstallationReceiptPath);
        await candidate.WriteJsonAsync(
            candidate.Layout.InstallationReceiptPath,
            receipt with { InstalledAtUtc = receipt.InstalledAtUtc.AddMinutes(1) });

        var result = await candidate.VerifyAsync();

        AssertVerificationFailed(result, DiagnosticIds.AcceptanceDigestMismatch);
    }

    [TestMethod]
    public async Task VerifyAsync_WhenWorkshopTextUsesLfRepresentation_ReturnsOnip5005()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();
        var original = await File.ReadAllTextAsync(candidate.Layout.DescriptionPath);
        await File.WriteAllTextAsync(
            candidate.Layout.DescriptionPath,
            original.Replace("\r\n", "\n", StringComparison.Ordinal),
            new UTF8Encoding(false));

        var result = await candidate.VerifyAsync();

        AssertVerificationFailed(result, DiagnosticIds.InvalidUploaderRepresentation);
    }

    [TestMethod]
    public async Task VerifyAsync_WhenObservedContentTamperIsLaterRestored_RemainsVerificationFailed()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();
        var original = await File.ReadAllBytesAsync(candidate.Layout.DescriptionPath);
        var first = await candidate.VerifyAsync();
        Assert.IsTrue(first.IsSuccess, CandidateFixture.Render(first.Diagnostics));
        await File.AppendAllTextAsync(candidate.Layout.DescriptionPath, "tamper");
        var tampered = await candidate.VerifyAsync();
        AssertVerificationFailed(tampered, DiagnosticIds.CandidateManifestMismatch);
        var irreversible = tampered.Value!.IrreversibleInvalidation;
        Assert.IsNotNull(irreversible);
        await File.WriteAllBytesAsync(candidate.Layout.DescriptionPath, original);

        var restored = await candidate.VerifyAsync();

        Assert.IsFalse(restored.IsSuccess);
        Assert.AreEqual(ReleaseCandidateState.VerificationFailed, restored.Value!.State);
        Assert.AreEqual(irreversible, restored.Value.IrreversibleInvalidation);
        Assert.IsTrue(restored.Diagnostics.Any(diagnostic =>
            diagnostic.Id == DiagnosticIds.ReleaseNotReady));
    }

    [TestMethod]
    public async Task VerifyAsync_WhenReadyCandidateIsVerifiedTwice_DerivedEvidenceIsByteIdentical()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();
        var first = await candidate.VerifyAsync();
        Assert.IsTrue(first.IsSuccess, CandidateFixture.Render(first.Diagnostics));
        var firstEvidence = await ReadDerivedEvidenceAsync(candidate.Layout);

        var second = await candidate.VerifyAsync();

        Assert.IsTrue(second.IsSuccess, CandidateFixture.Render(second.Diagnostics));
        var secondEvidence = await ReadDerivedEvidenceAsync(candidate.Layout);
        foreach (var path in firstEvidence.Keys)
        {
            CollectionAssert.AreEqual(firstEvidence[path], secondEvidence[path], path);
            Assert.AreEqual(
                CandidateFixture.Sha256(firstEvidence[path]),
                CandidateFixture.Sha256(secondEvidence[path]),
                path);
        }
    }

    [TestMethod]
    public async Task VerifyAsync_WhenReady_IndexesEveryFinalEvidenceFileExceptReadinessReport()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();

        var result = await candidate.VerifyAsync();

        Assert.IsTrue(result.IsSuccess, CandidateFixture.Render(result.Diagnostics));
        var report = result.Value!;
        var paths = report.EvidenceIndex.Select(entry => entry.Path).ToArray();
        CollectionAssert.AreEqual(
            paths.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
            paths);
        Assert.DoesNotContain(
            "release-evidence/release-readiness-report.json",
            paths);
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
        CollectionAssert.AreEqual(expectedPaths, paths);
        foreach (var entry in report.EvidenceIndex)
        {
            var absolutePath = ResolveCandidatePath(candidate.Root, entry.Path);
            var bytes = await File.ReadAllBytesAsync(absolutePath);
            Assert.AreEqual(bytes.LongLength, entry.ByteLength, entry.Path);
            Assert.AreEqual(
                Convert.ToHexStringLower(SHA256.HashData(bytes)),
                entry.Sha256,
                entry.Path);
        }
    }

    [TestMethod]
    public async Task VerifyAsync_WhenIndexedDerivedEvidenceIsEdited_FailsClosed()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();
        var first = await candidate.VerifyAsync();
        Assert.IsTrue(first.IsSuccess, CandidateFixture.Render(first.Diagnostics));
        await File.AppendAllTextAsync(candidate.Layout.ReleaseSummaryPath, "manual edit");

        var result = await candidate.VerifyAsync();

        AssertVerificationFailed(result, DiagnosticIds.ReleaseNotReady);
    }

    private static void AssertVerificationFailed(
        OperationResult<ReleaseReadinessReport> result,
        string diagnosticId)
    {
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PipelineExitCode.ReleaseNotReady, result.ExitCode);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual(ReleaseCandidateState.VerificationFailed, result.Value.State);
        Assert.IsNotNull(result.Value.IrreversibleInvalidation);
        Assert.IsTrue(result.Diagnostics.Any(diagnostic =>
            diagnostic.Id == diagnosticId),
            CandidateFixture.Render(result.Diagnostics));
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
