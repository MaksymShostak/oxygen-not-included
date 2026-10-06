using System.Text;
using System.Text.Json;
using MaksymShostak.OniModPipeline.Readme;

namespace MaksymShostak.OniModPipeline.Tests.Readme;

[TestClass]
public sealed class MarkdownPresentationTests
{
    private const string Start = "<!-- oni-mod-pipeline:workshop-description:start -->\n";
    private const string End = "<!-- oni-mod-pipeline:workshop-description:end -->\n";
    private const string Image = "![](https://raw.githubusercontent.com/MaksymShostak/oxygen-not-included/main/docs/badges/Dlc1Yes.png)";

    [TestMethod]
    public void Alternatives_UseNativeCoordinatesAndKeepOtherSyntaxAndOutsideBytes()
    {
        var candidate = "# Owner title\n" + Start + "# Section\n雪 " + Image + " " + Image + "\n```text\n# Literal\n" + Image + "\n```\n" + End + "Owner footer  \n";
        using var diagnostics = JsonDocument.Parse(JsonSerializer.Serialize(new[] {
            new { rule = "markdown/no-multiple-h1", line = 3, column = 1 },
            new { rule = "markdown/require-alt-text", line = 4, column = 3 },
            new { rule = "markdown/require-alt-text", line = 4, column = 4 + Image.Length }
        }));
        var output = Encoding.UTF8.GetString(InstalledMarkdownCanonicalizer.AddBadgeAlternatives(Encoding.UTF8.GetBytes(candidate), diagnostics.RootElement));
        var expected = candidate.Replace("雪 " + Image + " " + Image, "雪 " + Image.Replace("![]", "![Spaced Out! supported]", StringComparison.Ordinal) + " " + Image.Replace("![]", "![Spaced Out! supported]", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.AreEqual(expected, output);
    }

    [TestMethod]
    public void Alternatives_RejectDuplicateNativeCoordinates()
    {
        var candidate = Start + Image + "\n" + End;
        var diagnostic = new { rule = "markdown/require-alt-text", line = 2, column = 1 };
        using var diagnostics = JsonDocument.Parse(JsonSerializer.Serialize(new[] { diagnostic, diagnostic }));
        Assert.Throws<InvalidDataException>(() => InstalledMarkdownCanonicalizer.AddBadgeAlternatives(Encoding.UTF8.GetBytes(candidate), diagnostics.RootElement));
    }

    [TestMethod]
    public void Alternatives_RejectOutsideUnknownAndMismatchedCoordinatesWithoutGuessing()
    {
        var candidate = Image + "\n" + Start + "## Section\n![](https://example.org/unknown.png)\n" + End;
        foreach (var diagnostic in new[] {
            new { rule = "markdown/require-alt-text", line = 1, column = 1 },
            new { rule = "markdown/require-alt-text", line = 4, column = 1 },
            new { rule = "markdown/require-alt-text", line = 3, column = 2 },
            new { rule = "markdown/require-alt-text", line = 300, column = 1 }
        })
        {
            using var diagnostics = JsonDocument.Parse(JsonSerializer.Serialize(new[] { diagnostic }));
            Assert.Throws<InvalidDataException>(() => InstalledMarkdownCanonicalizer.AddBadgeAlternatives(Encoding.UTF8.GetBytes(candidate), diagnostics.RootElement));
        }
    }
}
