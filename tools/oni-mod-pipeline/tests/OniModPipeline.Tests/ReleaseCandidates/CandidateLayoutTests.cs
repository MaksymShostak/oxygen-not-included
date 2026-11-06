using MaksymShostak.OniModPipeline.ReleaseCandidates;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.ReleaseCandidates;
public sealed class CandidateLayoutTests
{
    [Test]
    public async Task Create_WhenSegmentsAreValid_DerivesEveryCandidateAndEvidencePath()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var layout = CandidateLayout.Create(
            temporaryDirectory.Path,
            "Example.Mod",
            "2026.8.27",
            "20260827T140302.1234567Z-0123456789abcdef");
        var expectedCandidate = Path.Combine(
            temporaryDirectory.Path,
            "release-candidates",
            "Example.Mod",
            "2026.8.27",
            "20260827T140302.1234567Z-0123456789abcdef");

        await Assert.That(layout.CandidateDirectory).IsEqualTo(Path.GetFullPath(expectedCandidate));
        await Assert.That(layout.WorkshopContentDirectory).IsEqualTo(Path.Combine(expectedCandidate, "workshop-content"));
        await Assert.That(layout.WorkshopListingDirectory).IsEqualTo(Path.Combine(expectedCandidate, "workshop-listing"));
        await Assert.That(layout.DescriptionPath).IsEqualTo(Path.Combine(expectedCandidate, "workshop-listing", "description.bbcode"));
        await Assert.That(layout.ChangeNotesPath).IsEqualTo(Path.Combine(expectedCandidate, "workshop-listing", "change-notes.bbcode"));
        await Assert.That(layout.ReleaseEvidenceDirectory).IsEqualTo(Path.Combine(expectedCandidate, "release-evidence"));
        await Assert.That(layout.ReleaseReadinessReportPath).IsEqualTo(Path.Combine(expectedCandidate, "release-evidence", "release-readiness-report.json"));
        await Assert.That(layout.ReleaseContentManifestPath).IsEqualTo(Path.Combine(expectedCandidate, "release-evidence", "release-content-manifest.json"));
        await Assert.That(layout.BuildProvenancePath).IsEqualTo(Path.Combine(expectedCandidate, "release-evidence", "build-provenance.json"));
        await Assert.That(layout.AutomatedTestResultsDirectory).IsEqualTo(Path.Combine(expectedCandidate, "release-evidence", "automated-test-results"));
        await Assert.That(layout.AcceptanceTestPlanPath).IsEqualTo(Path.Combine(expectedCandidate, "release-evidence", "acceptance-test-plan.json"));
        await Assert.That(layout.ReleaseSummaryPath).IsEqualTo(Path.Combine(expectedCandidate, "release-evidence", "release-summary.md"));
        await Assert.That(layout.UploaderChecklistPath).IsEqualTo(Path.Combine(expectedCandidate, "release-evidence", "uploader-checklist.md"));
        await Assert.That(layout.InstallationReceiptPath).IsEqualTo(Path.Combine(expectedCandidate, "release-evidence", "installation-receipt.json"));
        await Assert.That(layout.AcceptanceTestResultsPath).IsEqualTo(Path.Combine(expectedCandidate, "release-evidence", "acceptance-test-results.json"));
    }

    [Test]
    public async Task Create_WhenSegmentIsNotPortableAcrossSupportedHosts_RejectsInput()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        const string runId = "20260827T140302.1234567Z-0123456789abcdef";

        foreach (var invalidStaticId in new[] { "CON", "con.txt", "name:", "name. " })
        {
            await Assert.That(() => CandidateLayout.Create(
                temporaryDirectory.Path,
                invalidStaticId,
                "1.0.0",
                runId)).ThrowsExactly<ArgumentException>().Because(invalidStaticId);
        }
    }

    [Test]
    public async Task Create_WhenStaticIdOrVersionIsNotSingleSegment_RejectsInput()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        const string runId = "20260827T140302.1234567Z-0123456789abcdef";

        await Assert.That(() => CandidateLayout.Create(
            temporaryDirectory.Path,
            "../escape",
            "1.0.0",
            runId)).ThrowsExactly<ArgumentException>();
        await Assert.That(() => CandidateLayout.Create(
            temporaryDirectory.Path,
            "Example.Mod",
            "nested/version",
            runId)).ThrowsExactly<ArgumentException>();
        await Assert.That(() => CandidateLayout.Create(
            temporaryDirectory.Path,
            "Example.Mod",
            "..",
            runId)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task CreateTransientSibling_WhenGivenGuid_StaysUnderVersionDirectory()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var layout = CandidateLayout.Create(
            temporaryDirectory.Path,
            "Example.Mod",
            "1.0.0",
            "20260827T140302.1234567Z-0123456789abcdef");
        var guid = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

        var staging = layout.CreateTransientSiblingPath("staging", guid);
        var work = layout.CreateTransientSiblingPath("work", guid);

        await Assert.That(staging).IsEqualTo(Path.Combine(
                layout.VersionDirectory,
                ".20260827T140302.1234567Z-0123456789abcdef.staging-0123456789abcdef0123456789abcdef"));
        await Assert.That(work).IsEqualTo(Path.Combine(
                layout.VersionDirectory,
                ".20260827T140302.1234567Z-0123456789abcdef.work-0123456789abcdef0123456789abcdef"));
        await Assert.That(layout.IsOwnedTransientSibling(staging)).IsTrue();
        await Assert.That(layout.IsOwnedTransientSibling(work)).IsTrue();
        await Assert.That(layout.IsOwnedTransientSibling(layout.VersionDirectory)).IsFalse();
    }
}
