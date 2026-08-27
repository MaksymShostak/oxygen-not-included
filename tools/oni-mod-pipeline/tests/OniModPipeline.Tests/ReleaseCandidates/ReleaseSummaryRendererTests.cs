using MaksymShostak.OniModPipeline.ReleaseCandidates;

namespace MaksymShostak.OniModPipeline.Tests.ReleaseCandidates;

[TestClass]
public sealed class ReleaseSummaryRendererTests
{
    [TestMethod]
    public async Task VerifyAsync_WhenReady_RendersCompleteHumanUploaderSummary()
    {
        await using var candidate = await CandidateFixture.CreateReadyAsync();

        var result = await candidate.VerifyAsync();

        Assert.IsTrue(result.IsSuccess, CandidateFixture.Render(result.Diagnostics));
        var summary = await File.ReadAllTextAsync(candidate.Layout.ReleaseSummaryPath);
        Assert.Contains("Candidate state: `ready-for-upload`", summary, StringComparison.Ordinal);
        Assert.Contains("Static ID: `Example.Mod`", summary, StringComparison.Ordinal);
        Assert.Contains("Title: Example Mod", summary, StringComparison.Ordinal);
        Assert.Contains("Version: `1.2.3`", summary, StringComparison.Ordinal);
        Assert.Contains("Repository commit:", summary, StringComparison.Ordinal);
        Assert.Contains("Release-content digest:", summary, StringComparison.Ordinal);
        Assert.Contains(".NET SDK:", summary, StringComparison.Ordinal);
        Assert.Contains("ONI game build metadata:", summary, StringComparison.Ordinal);
        Assert.Contains("| Automated test | Required | Result | TRX |", summary, StringComparison.Ordinal);
        Assert.Contains("| Acceptance check | Required | Outcome | Note |", summary, StringComparison.Ordinal);
        Assert.Contains("First check", summary, StringComparison.Ordinal);
        Assert.Contains("Second check", summary, StringComparison.Ordinal);
        Assert.Contains(candidate.Layout.WorkshopContentDirectory, summary, StringComparison.Ordinal);
        Assert.Contains(candidate.Layout.DescriptionPath, summary, StringComparison.Ordinal);
        Assert.Contains(candidate.Layout.ChangeNotesPath, summary, StringComparison.Ordinal);
        Assert.Contains(
            Path.Combine(candidate.Layout.WorkshopListingDirectory, "preview.png"),
            summary,
            StringComparison.Ordinal);
        Assert.Contains("Preview format and size: `png`", summary, StringComparison.Ordinal);
        Assert.Contains("Mod types / tags: `tweaks`", summary, StringComparison.Ordinal);
        Assert.Contains("DLC compatibility: `Base Game`", summary, StringComparison.Ordinal);
        Assert.Contains(
            "Steam publication has not occurred.",
            summary,
            StringComparison.Ordinal);
    }
}
