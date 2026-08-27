using MaksymShostak.OniModPipeline.Tests.Fixtures;
using MaksymShostak.OniModPipeline.WorkshopListing;

namespace MaksymShostak.OniModPipeline.Tests.WorkshopListing;

[TestClass]
public sealed class PreviewImageInspectorTests
{
    [TestMethod]
    public void Inspect_WhenExtensionAndSignatureAgree_AcceptsSupportedFormats()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var fixtures = new[]
        {
            ("preview.PNG", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, ".png", "png"),
            ("preview.jpg", new byte[] { 0xFF, 0xD8, 0xFF }, ".jpg", "jpeg"),
            ("preview.jpeg", new byte[] { 0xFF, 0xD8, 0xFF }, ".jpg", "jpeg"),
            ("preview.gif", "GIF87a"u8.ToArray(), ".gif", "gif"),
            ("animation.GIF", "GIF89a"u8.ToArray(), ".gif", "gif")
        };
        var inspector = new PreviewImageInspector();

        foreach (var fixture in fixtures)
        {
            var path = temporaryDirectory.GetPath(fixture.Item1);
            File.WriteAllBytes(path, fixture.Item2);

            var result = inspector.Inspect(path);

            Assert.IsTrue(result.IsSuccess, fixture.Item1);
            Assert.AreEqual(fixture.Item3, result.Value?.CandidateExtension);
            Assert.AreEqual(fixture.Item4, result.Value?.Format);
            Assert.AreEqual((long)fixture.Item2.Length, result.Value?.ByteLength);
        }
    }

    [TestMethod]
    public void Inspect_WhenExtensionAndSignatureDisagree_ReturnsOnip1006()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var path = temporaryDirectory.GetPath("preview.png");
        File.WriteAllBytes(path, [0xFF, 0xD8, 0xFF]);

        var result = new PreviewImageInspector().Inspect(path);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("ONIP1006", result.Diagnostics.Single().Id);
    }

    [TestMethod]
    public void Inspect_WhenExtensionIsUnsupported_ReturnsOnip1006()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var path = temporaryDirectory.GetPath("preview.bmp");
        File.WriteAllBytes(path, [0x42, 0x4D, 0x00]);

        var result = new PreviewImageInspector().Inspect(path);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("ONIP1006", result.Diagnostics.Single().Id);
    }
}
