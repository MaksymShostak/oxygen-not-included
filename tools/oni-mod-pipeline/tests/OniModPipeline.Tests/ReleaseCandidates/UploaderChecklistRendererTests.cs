using MaksymShostak.OniModPipeline.ReleaseCandidates;

namespace MaksymShostak.OniModPipeline.Tests.ReleaseCandidates;
public sealed class UploaderChecklistRendererTests
{
    [Test]
    public async Task VerifyAsync_WhenReady_RendersExactHumanOnlyUploaderChecklist()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();

        var result = await candidate.VerifyAsync();

        await Assert.That(result.IsSuccess).IsTrue().Because(CandidateFixture.Render(result.Diagnostics));
        var checklist = await File.ReadAllTextAsync(candidate.Layout.UploaderChecklistPath);
        await Assert.That(checklist).Contains("[ ] Candidate state is ready-for-upload.", StringComparison.Ordinal);
        await Assert.That(checklist).Contains($"[ ] Update Data points exactly to `{candidate.Layout.WorkshopContentDirectory}`.", StringComparison.Ordinal);
        await Assert.That(checklist).Contains("[ ] The displayed data path is not the mutable Dev/Local test directory.", StringComparison.Ordinal);
        await Assert.That(checklist).Contains($"[ ] Description comes from `{candidate.Layout.DescriptionPath}`.", StringComparison.Ordinal);
        await Assert.That(checklist).Contains("[ ] Paragraphs, blank lines, ---, headings, and [list] blocks remain separate after paste.", StringComparison.Ordinal);
        await Assert.That(checklist).Contains($"[ ] Change notes come from `{candidate.Layout.ChangeNotesPath}`.", StringComparison.Ordinal);
        await Assert.That(checklist).Contains($"[ ] Preview comes from `{Path.Combine(candidate.Layout.WorkshopListingDirectory, "preview.png")}`.", StringComparison.Ordinal);
        await Assert.That(checklist).Contains("[ ] Title, mod types, tags, and DLC compatibility match release-summary.md.", StringComparison.Ordinal);
        await Assert.That(checklist).Contains("[ ] The final form has been reviewed immediately before Publish.", StringComparison.Ordinal);
        await Assert.That(checklist).EndsWith("Publish is a deliberate authenticated human action. ONI Mod Pipeline does not perform or record it.\n", StringComparison.Ordinal);
        await Assert.That(checklist).DoesNotContain("Publication remains blocked", StringComparison.Ordinal);
    }
}
