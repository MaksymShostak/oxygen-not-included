using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.FixturesTests;

[TestClass]
public sealed class TemporaryDirectoryTests
{
    [TestMethod]
    public void Dispose_WhenDescendantFileIsReadOnly_RemovesValidatedTemporaryTree()
    {
        var temporaryDirectory = new TemporaryDirectory();
        var temporaryPath = temporaryDirectory.Path;
        var filePath = temporaryDirectory.GetPath("readonly.txt");
        File.WriteAllText(filePath, "content");
        File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.ReadOnly);

        temporaryDirectory.Dispose();

        Assert.IsFalse(Directory.Exists(temporaryPath));
    }
}
