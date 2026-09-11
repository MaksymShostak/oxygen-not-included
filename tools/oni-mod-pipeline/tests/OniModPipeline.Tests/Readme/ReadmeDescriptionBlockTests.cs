using System.Text;
using MaksymShostak.OniModPipeline.Readme;

namespace MaksymShostak.OniModPipeline.Tests.Readme;

[TestClass]
public sealed class ReadmeDescriptionBlockTests
{
    private const string Start = "<!-- oni-mod-pipeline:workshop-description:start -->";
    private const string End = "<!-- oni-mod-pipeline:workshop-description:end -->";

    [TestMethod]
    [DataRow("\n")]
    [DataRow("\r\n")]
    public void Replace_PreservesSurroundingBytesAndIsIdempotent(string newline)
    {
        var prefix = "\uFEFF# Mod 🐱\nSupport bytes  \r\n" + Start + newline;
        var suffix = End + "\n## Support\r\nUntouched  ";
        var original = Encoding.UTF8.GetBytes(prefix + "old" + newline + suffix);
        var result = ReadmeDescriptionBlock.Replace(original, "# Description\n\n- One\n- Two\n");
        var expected = Encoding.UTF8.GetBytes(prefix + ("# Description\n\n- One\n- Two\n").Replace("\n", newline) + suffix);
        CollectionAssert.AreEqual(expected, result);
        CollectionAssert.AreEqual(result, ReadmeDescriptionBlock.Replace(result, "# Description\n\n- One\n- Two\n"));
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow(End + "\n" + Start + "\n")]
    [DataRow(Start + "\n" + Start + "\n" + End)]
    [DataRow(Start + "\n" + End + "\n" + End)]
    [DataRow("prefix " + Start + "\n" + End)]
    [DataRow(Start + " suffix\n" + End)]
    [DataRow(Start + "\n" + End + " suffix")]
    public void Replace_RejectsInvalidMarkers(string original)
    {
        Assert.Throws<InvalidDataException>(() => ReadmeDescriptionBlock.Replace(Encoding.UTF8.GetBytes(original), "new"));
    }

    [TestMethod]
    public void Replace_RejectsGeneratedMarkersAndInvalidUtf8()
    {
        var original = Encoding.UTF8.GetBytes(Start + "\n" + End);
        Assert.Throws<InvalidDataException>(() => ReadmeDescriptionBlock.Replace(original, Start));
        Assert.Throws<DecoderFallbackException>(() => ReadmeDescriptionBlock.Replace([0xff], "new"));
    }
}
