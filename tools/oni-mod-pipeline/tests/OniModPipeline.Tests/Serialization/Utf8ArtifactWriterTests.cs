using MaksymShostak.OniModPipeline.Serialization;
using MaksymShostak.OniModPipeline.Tests.Fixtures;
using System.Text;
using System.Text.Json;

namespace MaksymShostak.OniModPipeline.Tests.Serialization;

[TestClass]
public sealed class Utf8ArtifactWriterTests
{
    [TestMethod]
    public async Task WriteJsonAtomicallyAsync_WritesCamelCaseUtf8WithoutBomAndOneFinalLf()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var destination = temporaryDirectory.GetPath("result.json");
        var writer = new Utf8ArtifactWriter();

        await writer.WriteJsonAtomicallyAsync(
            destination,
            new SampleArtifact("first\r\nsecond", 3),
            CancellationToken.None);

        var bytes = await File.ReadAllBytesAsync(destination);
        Assert.IsFalse(bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.AreEqual((byte)'\n', bytes[^1]);
        Assert.AreNotEqual((byte)'\n', bytes[^2]);
        Assert.IsFalse(bytes.Contains((byte)'\r'));
        using var document = JsonDocument.Parse(bytes);
        Assert.AreEqual(
            "first\r\nsecond",
            document.RootElement.GetProperty("displayName").GetString());
        Assert.AreEqual(3, document.RootElement.GetProperty("itemCount").GetInt32());
    }

    [TestMethod]
    public async Task WriteLfTextAtomicallyAsync_NormalizesLineEndingsAndOneFinalLf()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var destination = temporaryDirectory.GetPath("notes.bbcode");
        var writer = new Utf8ArtifactWriter();

        await writer.WriteLfTextAtomicallyAsync(
            destination,
            "alpha\r\nbeta\rgamma\n\n",
            CancellationToken.None);

        var bytes = await File.ReadAllBytesAsync(destination);
        CollectionAssert.AreEqual(
            Encoding.UTF8.GetBytes("alpha\nbeta\ngamma\n"),
            bytes);
    }

    [TestMethod]
    public async Task WriteLfTextAtomicallyAsync_ReplacesOnlyNamedDestination()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var destination = temporaryDirectory.GetPath("derived.txt");
        var sibling = temporaryDirectory.GetPath("immutable.txt");
        await File.WriteAllTextAsync(destination, "old\n");
        await File.WriteAllTextAsync(sibling, "keep\n");
        var writer = new Utf8ArtifactWriter();

        await writer.WriteLfTextAtomicallyAsync(
            destination,
            "new",
            CancellationToken.None);

        Assert.AreEqual("new\n", await File.ReadAllTextAsync(destination));
        Assert.AreEqual("keep\n", await File.ReadAllTextAsync(sibling));
        CollectionAssert.AreEquivalent(
            new[] { "derived.txt", "immutable.txt" },
            Directory.EnumerateFiles(temporaryDirectory.Path)
                .Select(Path.GetFileName)
                .ToArray());
    }

    [TestMethod]
    public async Task WriteJsonAtomicallyAsync_WhenSerializationFails_LeavesDestinationUnchanged()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var destination = temporaryDirectory.GetPath("result.json");
        var original = Encoding.UTF8.GetBytes("{\"state\":\"original\"}\n");
        await File.WriteAllBytesAsync(destination, original);
        var writer = new Utf8ArtifactWriter();

        await Assert.ThrowsExactlyAsync<NotSupportedException>(() =>
            writer.WriteJsonAtomicallyAsync(
                destination,
                new UnsupportedArtifact(() => { }),
                CancellationToken.None));

        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(destination));
        CollectionAssert.AreEqual(
            new[] { destination },
            Directory.EnumerateFiles(temporaryDirectory.Path).ToArray());
    }

    private sealed record SampleArtifact(string DisplayName, int ItemCount);

    private sealed record UnsupportedArtifact(Action Callback);
}
