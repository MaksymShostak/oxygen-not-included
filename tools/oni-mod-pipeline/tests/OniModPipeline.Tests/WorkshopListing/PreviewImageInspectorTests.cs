using MaksymShostak.OniModPipeline.Tests.Fixtures;
using MaksymShostak.OniModPipeline.WorkshopListing;

namespace MaksymShostak.OniModPipeline.Tests.WorkshopListing;
public sealed class PreviewImageInspectorTests
{
    [Test]
    public async Task Inspect_WhenExtensionAndSignatureAgree_AcceptsSupportedFormats()
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

            await Assert.That(result.IsSuccess).IsTrue().Because(fixture.Item1);
            await Assert.That(result.Value?.CandidateExtension).IsEqualTo(fixture.Item3);
            await Assert.That(result.Value?.Format).IsEqualTo(fixture.Item4);
            await Assert.That(result.Value?.ByteLength).IsEqualTo((long)fixture.Item2.Length);
        }
    }

    [Test]
    public async Task Inspect_WhenExtensionAndSignatureDisagree_ReturnsOnip1006()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var path = temporaryDirectory.GetPath("preview.png");
        File.WriteAllBytes(path, [0xFF, 0xD8, 0xFF]);

        var result = new PreviewImageInspector().Inspect(path);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Diagnostics.Single().Id).IsEqualTo("ONIP1006");
    }

    [Test]
    public async Task Inspect_WhenExtensionIsUnsupported_ReturnsOnip1006()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var path = temporaryDirectory.GetPath("preview.bmp");
        File.WriteAllBytes(path, [0x42, 0x4D, 0x00]);

        var result = new PreviewImageInspector().Inspect(path);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Diagnostics.Single().Id).IsEqualTo("ONIP1006");
    }
}
