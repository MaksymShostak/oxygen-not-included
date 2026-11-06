using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.FixturesTests;
public sealed class TemporaryDirectoryTests
{
    [Test]
    public async Task Dispose_WhenDescendantFileIsReadOnly_RemovesValidatedTemporaryTree()
    {
        var temporaryDirectory = new TemporaryDirectory();
        var temporaryPath = temporaryDirectory.Path;
        var filePath = temporaryDirectory.GetPath("readonly.txt");
        File.WriteAllText(filePath, "content");
        File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.ReadOnly);

        temporaryDirectory.Dispose();

        await Assert.That(Directory.Exists(temporaryPath)).IsFalse();
    }
}
