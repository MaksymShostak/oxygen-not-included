using MaksymShostak.OniModPipeline.Cli;

namespace MaksymShostak.OniModPipeline.Tests.Cli;
public sealed class SyncReadmeCommandTests
{
    [Test]
    public async Task Parse_AcceptsSyncReadmeAndInstalledPackageDirectory()
    {
        var result = CliApplication.CreateRootCommand().Parse(["sync-readme", "--mod", "mod path", "--converter-package", "installed package", "--check", "--format", "json"]);
        await Assert.That(result.Errors.Count).IsEqualTo(0).Because(string.Join("; ", result.Errors.Select(error => error.Message)));
    }
}
