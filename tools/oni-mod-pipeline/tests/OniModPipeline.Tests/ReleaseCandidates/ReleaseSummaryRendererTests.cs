using MaksymShostak.OniModPipeline.ReleaseCandidates;

namespace MaksymShostak.OniModPipeline.Tests.ReleaseCandidates;
public sealed class ReleaseSummaryRendererTests
{
    [Test]
    public async Task VerifyAsync_WhenReady_RendersCompleteHumanUploaderSummary()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();

        var result = await candidate.VerifyAsync();

        await Assert.That(result.IsSuccess).IsTrue().Because(CandidateFixture.Render(result.Diagnostics));
        var summary = await File.ReadAllTextAsync(candidate.Layout.ReleaseSummaryPath);
        await Assert.That(summary).Contains("Candidate state: `ready-for-upload`", StringComparison.Ordinal);
        await Assert.That(summary).Contains("Static ID: `Example.Mod`", StringComparison.Ordinal);
        await Assert.That(summary).Contains("Title: Example Mod", StringComparison.Ordinal);
        await Assert.That(summary).Contains("Version: `1.2.3`", StringComparison.Ordinal);
        await Assert.That(summary).Contains("Repository commit:", StringComparison.Ordinal);
        await Assert.That(summary).Contains("Release-content digest:", StringComparison.Ordinal);
        await Assert.That(summary).Contains(".NET SDK:", StringComparison.Ordinal);
        await Assert.That(summary).Contains("ONI game build metadata:", StringComparison.Ordinal);
        await Assert.That(summary).Contains("| Automated test | Required | Result | TRX |", StringComparison.Ordinal);
        await Assert.That(summary).Contains("| Acceptance check | Required | Outcome | Note |", StringComparison.Ordinal);
        await Assert.That(summary).Contains("First check", StringComparison.Ordinal);
        await Assert.That(summary).Contains("Second check", StringComparison.Ordinal);
        await Assert.That(summary).Contains(candidate.Layout.WorkshopContentDirectory, StringComparison.Ordinal);
        await Assert.That(summary).Contains(candidate.Layout.DescriptionPath, StringComparison.Ordinal);
        await Assert.That(summary).Contains(candidate.Layout.ChangeNotesPath, StringComparison.Ordinal);
        await Assert.That(summary).Contains(Path.Combine(candidate.Layout.WorkshopListingDirectory, "preview.png"), StringComparison.Ordinal);
        await Assert.That(summary).Contains("Preview format and size: `png`", StringComparison.Ordinal);
        await Assert.That(summary).Contains("Mod types / tags: `tweaks`", StringComparison.Ordinal);
        await Assert.That(summary).Contains("DLC compatibility: `Base Game`", StringComparison.Ordinal);
        await Assert.That(summary).Contains("Steam publication has not occurred.", StringComparison.Ordinal);
    }
}
