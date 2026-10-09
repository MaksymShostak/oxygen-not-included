using MaksymShostak.OniModPipeline.Readme;
using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.Readme;

[TestClass]
public sealed class CompatibilityBadgeLabelsTests
{
    [TestMethod]
    public async Task GeneratedDescription_UsesBoundedStdinAtTheDomainRenderer()
    {
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(directory.GetPath("tooling", "markdown"));
        File.WriteAllText(directory.GetPath("tooling", "markdown", "render-description.mjs"), "// renderer");
        var runner = new Runner();
        Assert.AreEqual("rendered", await CompatibilityBadgeLabels.RenderGeneratedDescriptionAsync(directory.Path, "literal", runner, CancellationToken.None));
        Assert.AreEqual("literal", runner.Request!.StandardInput);
        Assert.AreEqual(2_097_152, runner.Request.OutputLimitCharacters);
    }

    [TestMethod]
    public async Task GeneratedDescription_RefusesUnsafeMissingRenderer()
    {
        using var directory = new TemporaryDirectory();
        var runner = new Runner();
        await Assert.ThrowsAsync<InvalidDataException>(() => CompatibilityBadgeLabels.RenderGeneratedDescriptionAsync(directory.Path, "literal", runner, CancellationToken.None));
        Assert.IsNull(runner.Request);
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
