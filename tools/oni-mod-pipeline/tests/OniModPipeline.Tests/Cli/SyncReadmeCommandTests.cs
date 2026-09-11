using MaksymShostak.OniModPipeline.Cli;

namespace MaksymShostak.OniModPipeline.Tests.Cli;

[TestClass]
public sealed class SyncReadmeCommandTests
{
    [TestMethod]
    public void Parse_AcceptsSyncReadmeAndInstalledPackageDirectory()
    {
        var result = CliApplication.CreateRootCommand().Parse(["sync-readme", "--mod", "mod path", "--converter-package", "installed package", "--check", "--format", "json"]);
        Assert.AreEqual(0, result.Errors.Count, string.Join("; ", result.Errors.Select(error => error.Message)));
    }
}
