using MaksymShostak.OniModPipeline.Serialization;
using MaksymShostak.OniModPipeline.Tests.Fixtures;
using System.Text;
using System.Text.Json;

namespace MaksymShostak.OniModPipeline.Tests.Serialization;
public sealed class Utf8ArtifactWriterTests
{
    [Test]
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
        await Assert.That(bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })).IsFalse();
        await Assert.That(bytes[^1]).IsEqualTo((byte)'\n');
        await Assert.That(bytes[^2]).IsNotEqualTo((byte)'\n');
        await Assert.That(bytes.Contains((byte)'\r')).IsFalse();
        using var document = JsonDocument.Parse(bytes);
        await Assert.That(document.RootElement.GetProperty("displayName").GetString()).IsEqualTo("first\r\nsecond");
        await Assert.That(document.RootElement.GetProperty("itemCount").GetInt32()).IsEqualTo(3);
    }

    [Test]
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
        await Assert.That(bytes).IsEquivalentTo(Encoding.UTF8.GetBytes("alpha\nbeta\ngamma\n"), TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
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

        await Assert.That(await File.ReadAllTextAsync(destination)).IsEqualTo("new\n");
        await Assert.That(await File.ReadAllTextAsync(sibling)).IsEqualTo("keep\n");
        await Assert.That(Directory.EnumerateFiles(temporaryDirectory.Path)
                .Select(Path.GetFileName)
                .ToArray()).IsEquivalentTo((new[] { "derived.txt", "immutable.txt" }).Cast<string?>(), TUnit.Assertions.Enums.CollectionOrdering.Any);
    }

    [Test]
    public async Task WriteJsonAtomicallyAsync_WhenSerializationFails_LeavesDestinationUnchanged()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var destination = temporaryDirectory.GetPath("result.json");
        var original = Encoding.UTF8.GetBytes("{\"state\":\"original\"}\n");
        await File.WriteAllBytesAsync(destination, original);
        var writer = new Utf8ArtifactWriter();

        await Assert.That(async () =>
{await writer.WriteJsonAtomicallyAsync(
                destination,
                new UnsupportedArtifact(() => { }),
                CancellationToken.None);}).ThrowsExactly<NotSupportedException>();

        await Assert.That(await File.ReadAllBytesAsync(destination)).IsEquivalentTo(original, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Directory.EnumerateFiles(temporaryDirectory.Path).ToArray()).IsEquivalentTo(new[] { destination }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    private sealed record SampleArtifact(string DisplayName, int ItemCount);

    private sealed record UnsupportedArtifact(Action Callback);
}
