using System.Text;
using MaksymShostak.OniModPipeline.Readme;

namespace MaksymShostak.OniModPipeline.Tests.Readme;
public sealed class ReadmeDescriptionBlockTests
{
    private const string Start = "<!-- oni-mod-pipeline:workshop-description:start -->";
    private const string End = "<!-- oni-mod-pipeline:workshop-description:end -->";

    [Test]
    [Arguments("\n")]
    [Arguments("\r\n")]
    public async Task Replace_PreservesSurroundingBytesAndIsIdempotent(string newline)
    {
        var prefix = "\uFEFF# Mod 🐱\nSupport bytes  \r\n" + Start + newline;
        var suffix = End + "\n## Support\r\nUntouched  ";
        var original = Encoding.UTF8.GetBytes(prefix + "old" + newline + suffix);
        var result = ReadmeDescriptionBlock.Replace(original, "# Description\n\n- One\n- Two\n");
        var expected = Encoding.UTF8.GetBytes(prefix + ("# Description\n\n- One\n- Two\n").Replace("\n", newline) + suffix);
        await Assert.That(result).IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(ReadmeDescriptionBlock.Replace(result, "# Description\n\n- One\n- Two\n")).IsEquivalentTo(result, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("missing")]
    [Arguments(End + "\n" + Start + "\n")]
    [Arguments(Start + "\n" + Start + "\n" + End)]
    [Arguments(Start + "\n" + End + "\n" + End)]
    [Arguments("prefix " + Start + "\n" + End)]
    [Arguments(Start + " suffix\n" + End)]
    [Arguments(Start + "\n" + End + " suffix")]
    public async Task Replace_RejectsInvalidMarkers(string original)
    {
        await Assert.That(() => ReadmeDescriptionBlock.Replace(Encoding.UTF8.GetBytes(original), "new")).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Replace_RejectsGeneratedMarkersAndInvalidUtf8()
    {
        var original = Encoding.UTF8.GetBytes(Start + "\n" + End);
        await Assert.That(() => ReadmeDescriptionBlock.Replace(original, Start)).Throws<InvalidDataException>();
        await Assert.That(() => ReadmeDescriptionBlock.Replace([0xff], "new")).Throws<DecoderFallbackException>();
    }
}
