using MaksymShostak.OniModPipeline.ReleaseCandidates;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.ReleaseCandidates;

[TestClass]
public sealed class CandidateLayoutTests
{
    [TestMethod]
    public void Create_WhenSegmentsAreValid_DerivesEveryCandidateAndEvidencePath()
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

        Assert.AreEqual(Path.GetFullPath(expectedCandidate), layout.CandidateDirectory);
        Assert.AreEqual(
            Path.Combine(expectedCandidate, "workshop-content"),
            layout.WorkshopContentDirectory);
        Assert.AreEqual(
            Path.Combine(expectedCandidate, "workshop-listing"),
            layout.WorkshopListingDirectory);
        Assert.AreEqual(
            Path.Combine(expectedCandidate, "workshop-listing", "description.bbcode"),
            layout.DescriptionPath);
        Assert.AreEqual(
            Path.Combine(expectedCandidate, "workshop-listing", "change-notes.bbcode"),
            layout.ChangeNotesPath);
        Assert.AreEqual(
            Path.Combine(expectedCandidate, "release-evidence"),
            layout.ReleaseEvidenceDirectory);
        Assert.AreEqual(
            Path.Combine(expectedCandidate, "release-evidence", "release-readiness-report.json"),
            layout.ReleaseReadinessReportPath);
        Assert.AreEqual(
            Path.Combine(expectedCandidate, "release-evidence", "release-content-manifest.json"),
            layout.ReleaseContentManifestPath);
        Assert.AreEqual(
            Path.Combine(expectedCandidate, "release-evidence", "build-provenance.json"),
            layout.BuildProvenancePath);
        Assert.AreEqual(
            Path.Combine(expectedCandidate, "release-evidence", "automated-test-results"),
            layout.AutomatedTestResultsDirectory);
        Assert.AreEqual(
            Path.Combine(expectedCandidate, "release-evidence", "acceptance-test-plan.json"),
            layout.AcceptanceTestPlanPath);
        Assert.AreEqual(
            Path.Combine(expectedCandidate, "release-evidence", "release-summary.md"),
            layout.ReleaseSummaryPath);
        Assert.AreEqual(
            Path.Combine(expectedCandidate, "release-evidence", "uploader-checklist.md"),
            layout.UploaderChecklistPath);
        Assert.AreEqual(
            Path.Combine(expectedCandidate, "release-evidence", "installation-receipt.json"),
            layout.InstallationReceiptPath);
        Assert.AreEqual(
            Path.Combine(expectedCandidate, "release-evidence", "acceptance-test-results.json"),
            layout.AcceptanceTestResultsPath);
    }

    [TestMethod]
    public void Create_WhenSegmentIsNotPortableAcrossSupportedHosts_RejectsInput()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        const string runId = "20260827T140302.1234567Z-0123456789abcdef";

        foreach (var invalidStaticId in new[] { "CON", "con.txt", "name:", "name. " })
        {
            Assert.ThrowsExactly<ArgumentException>(() => CandidateLayout.Create(
                temporaryDirectory.Path,
                invalidStaticId,
                "1.0.0",
                runId),
                invalidStaticId);
        }
    }

    [TestMethod]
    public void Create_WhenStaticIdOrVersionIsNotSingleSegment_RejectsInput()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        const string runId = "20260827T140302.1234567Z-0123456789abcdef";

        Assert.ThrowsExactly<ArgumentException>(() => CandidateLayout.Create(
            temporaryDirectory.Path,
            "../escape",
            "1.0.0",
            runId));
        Assert.ThrowsExactly<ArgumentException>(() => CandidateLayout.Create(
            temporaryDirectory.Path,
            "Example.Mod",
            "nested/version",
            runId));
        Assert.ThrowsExactly<ArgumentException>(() => CandidateLayout.Create(
            temporaryDirectory.Path,
            "Example.Mod",
            "..",
            runId));
    }

    [TestMethod]
    public void CreateTransientSibling_WhenGivenGuid_StaysUnderVersionDirectory()
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

        Assert.AreEqual(
            Path.Combine(
                layout.VersionDirectory,
                ".20260827T140302.1234567Z-0123456789abcdef.staging-0123456789abcdef0123456789abcdef"),
            staging);
        Assert.AreEqual(
            Path.Combine(
                layout.VersionDirectory,
                ".20260827T140302.1234567Z-0123456789abcdef.work-0123456789abcdef0123456789abcdef"),
            work);
        Assert.IsTrue(layout.IsOwnedTransientSibling(staging));
        Assert.IsTrue(layout.IsOwnedTransientSibling(work));
        Assert.IsFalse(layout.IsOwnedTransientSibling(layout.VersionDirectory));
    }
}
