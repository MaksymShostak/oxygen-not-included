using MaksymShostak.OniModPipeline.Readme;
using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.Readme;
public sealed class CompatibilityBadgeLabelsTests
{
    [Test]
    public async Task GeneratedDescription_UsesBoundedStdinAtTheDomainRenderer()
    {
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(directory.GetPath("tools", "readme"));
        File.WriteAllText(directory.GetPath("tools", "readme", "render-description.mjs"), "// renderer");
        var runner = new Runner();
        await Assert.That(await CompatibilityBadgeLabels.RenderGeneratedDescriptionAsync(directory.Path, "literal", runner, CancellationToken.None)).IsEqualTo("rendered");
        await Assert.That(runner.Request!.StandardInput).IsEqualTo("literal");
        await Assert.That(runner.Request.OutputLimitCharacters).IsEqualTo(2_097_152);
    }

    [Test]
    public async Task GeneratedDescription_RefusesUnsafeMissingRenderer()
    {
        using var directory = new TemporaryDirectory();
        var runner = new Runner();
        await Assert.That(async () => {await CompatibilityBadgeLabels.RenderGeneratedDescriptionAsync(directory.Path, "literal", runner, CancellationToken.None);}).Throws<InvalidDataException>();
        await Assert.That(runner.Request).IsNull();
    }

    private sealed class Runner : IExternalProcessRunner
    {
        internal ProcessRequest? Request { get; private set; }
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken token)
        {
            Request = request;
            return Task.FromResult(new ProcessResult(0, "rendered", ""));
        }
    }
}
