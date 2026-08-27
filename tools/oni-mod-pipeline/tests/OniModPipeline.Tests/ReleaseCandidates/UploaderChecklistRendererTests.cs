using MaksymShostak.OniModPipeline.ReleaseCandidates;

namespace MaksymShostak.OniModPipeline.Tests.ReleaseCandidates;

[TestClass]
public sealed class UploaderChecklistRendererTests
{
    [TestMethod]
    public async Task VerifyAsync_WhenReady_RendersExactHumanOnlyUploaderChecklist()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();

        var result = await candidate.VerifyAsync();

        Assert.IsTrue(result.IsSuccess, CandidateFixture.Render(result.Diagnostics));
        var checklist = await File.ReadAllTextAsync(candidate.Layout.UploaderChecklistPath);
        Assert.Contains(
            "[ ] Candidate state is ready-for-upload.",
            checklist,
            StringComparison.Ordinal);
        Assert.Contains(
            $"[ ] Update Data points exactly to `{candidate.Layout.WorkshopContentDirectory}`.",
            checklist,
            StringComparison.Ordinal);
        Assert.Contains(
            "[ ] The displayed data path is not the mutable Dev/Local test directory.",
            checklist,
            StringComparison.Ordinal);
        Assert.Contains(
            $"[ ] Description comes from `{candidate.Layout.DescriptionPath}`.",
            checklist,
            StringComparison.Ordinal);
        Assert.Contains(
            "[ ] Paragraphs, blank lines, ---, headings, and [list] blocks remain separate after paste.",
            checklist,
            StringComparison.Ordinal);
        Assert.Contains(
            $"[ ] Change notes come from `{candidate.Layout.ChangeNotesPath}`.",
            checklist,
            StringComparison.Ordinal);
        Assert.Contains(
            $"[ ] Preview comes from `{Path.Combine(candidate.Layout.WorkshopListingDirectory, "preview.png")}`.",
            checklist,
            StringComparison.Ordinal);
        Assert.Contains(
            "[ ] Title, mod types, tags, and DLC compatibility match release-summary.md.",
            checklist,
            StringComparison.Ordinal);
        Assert.Contains(
            "[ ] The final form has been reviewed immediately before Publish.",
            checklist,
            StringComparison.Ordinal);
        Assert.EndsWith(
            "Publish is a deliberate authenticated human action. ONI Mod Pipeline does not perform or record it.\n",
            checklist,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Publication remains blocked",
            checklist,
            StringComparison.Ordinal);
    }
}
