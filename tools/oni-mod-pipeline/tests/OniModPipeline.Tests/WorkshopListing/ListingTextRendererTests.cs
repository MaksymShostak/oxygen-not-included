using MaksymShostak.OniModPipeline.WorkshopListing;
using System.Security.Cryptography;
using System.Text;

namespace MaksymShostak.OniModPipeline.Tests.WorkshopListing;

[TestClass]
public sealed class ListingTextRendererTests
{
    [TestMethod]
    [DataRow("lf")]
    [DataRow("crlf")]
    [DataRow("cr")]
    [DataRow("mixed")]
    public void Render_WhenInputUsesAnyLineBreakStyle_PreservesOneLogicalDocument(
        string variant)
    {
        var expected = ReadStructuralFixture();
        var input = CreateLineEndingVariant(expected, variant);
        var renderer = new ListingTextRenderer();

        var rendered = renderer.Render(input);

        CollectionAssert.AreEqual(
            Encoding.UTF8.GetBytes(expected.Replace("\n", "\r\n", StringComparison.Ordinal)),
            rendered.Bytes);
        Assert.AreEqual("utf-8", rendered.Report.Encoding);
        Assert.IsFalse(rendered.Report.HasBom);
        Assert.AreEqual("crlf", rendered.Report.LineEndings);
        Assert.AreEqual(2, rendered.Report.BlankLineCount);
    }

    [TestMethod]
    public void Render_WhenCalledTwice_IsLogicallyIdempotent()
    {
        var renderer = new ListingTextRenderer();
        var first = renderer.Render(ReadStructuralFixture());

        var second = renderer.Render(Encoding.UTF8.GetString(first.Bytes));

        CollectionAssert.AreEqual(first.Bytes, second.Bytes);
        Assert.AreEqual(
            first.Report.LogicalContentSha256,
            second.Report.LogicalContentSha256);
        Assert.AreEqual(first.Report.ArtifactSha256, second.Report.ArtifactSha256);
    }

    [TestMethod]
    public void Render_WhenArtifactIsWritten_UsesNoBomAndOnlyCrLfWithOneFinalCrLf()
    {
        var renderer = new ListingTextRenderer();

        var rendered = renderer.Render("alpha\r\n\r\nbeta\r\r\n");

        Assert.IsFalse(rendered.Bytes.AsSpan().StartsWith(
            new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.IsTrue(rendered.Bytes.AsSpan().EndsWith("\r\n"u8));
        Assert.IsFalse(rendered.Bytes.AsSpan().EndsWith("\r\n\r\n"u8));
        Assert.AreEqual(3, CountCrLfPairs(rendered.Bytes));
        AssertNoLoneLineEndings(rendered.Bytes);
        Assert.AreEqual(4, rendered.Report.LogicalLineCount);
        Assert.AreEqual(3, rendered.Report.LineBreakCount);
        Assert.AreEqual(1, rendered.Report.BlankLineCount);
    }

    [TestMethod]
    public void Render_WhenGivenRealDescription_PreservesDocumentContract()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourcePath = Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled",
            "STEAM_DESCRIPTION.bbcode");
        var sourceBytes = File.ReadAllBytes(sourcePath);
        Assert.IsFalse(sourceBytes.Contains((byte)'\r'));
        Assert.AreEqual((byte)'\n', sourceBytes[^1]);
        Assert.AreNotEqual((byte)'\n', sourceBytes[^2]);
        var renderer = new ListingTextRenderer();

        var rendered = renderer.Render(Encoding.UTF8.GetString(sourceBytes));
        var roundTrip = renderer.Render(Encoding.UTF8.GetString(rendered.Bytes));

        Assert.AreEqual(54, rendered.Report.LogicalLineCount);
        Assert.AreEqual(53, rendered.Report.LineBreakCount);
        Assert.AreEqual(53, CountCrLfPairs(rendered.Bytes));
        AssertNoLoneLineEndings(rendered.Bytes);
        Assert.AreEqual(
            Convert.ToHexStringLower(SHA256.HashData(sourceBytes)),
            rendered.Report.LogicalContentSha256);
        Assert.AreEqual(
            rendered.Report.LogicalContentSha256,
            roundTrip.Report.LogicalContentSha256);
    }

    private static string ReadStructuralFixture()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "oni-mod-pipeline",
            "tests",
            "OniModPipeline.Tests",
            "Fixtures",
            "workshop-description-structure.bbcode");
        var text = File.ReadAllText(path, Encoding.UTF8);
        Assert.IsTrue(text.EndsWith('\n'));
        Assert.IsFalse(text.Contains('\r'));
        Assert.IsFalse(text.EndsWith("\n\n", StringComparison.Ordinal));
        return text;
    }

    private static string CreateLineEndingVariant(string source, string variant)
    {
        var withoutFinalLf = source[..^1];
        return variant switch
        {
            "lf" => source,
            "crlf" => withoutFinalLf.Replace("\n", "\r\n", StringComparison.Ordinal) + "\r\n",
            "cr" => withoutFinalLf.Replace('\n', '\r') + "\r",
            "mixed" => CreateMixedLineEndings(source),
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null)
        };
    }

    private static string CreateMixedLineEndings(string source)
    {
        var builder = new StringBuilder();
        var lineBreakIndex = 0;
        foreach (var character in source)
        {
            if (character != '\n')
            {
                builder.Append(character);
                continue;
            }

            builder.Append((lineBreakIndex++ % 3) switch
            {
                0 => "\n",
                1 => "\r\n",
                _ => "\r"
            });
        }

        return builder.ToString();
    }

    private static int CountCrLfPairs(ReadOnlySpan<byte> bytes)
    {
        var count = 0;
        for (var index = 0; index < bytes.Length - 1; index++)
        {
            if (bytes[index] == (byte)'\r' && bytes[index + 1] == (byte)'\n')
            {
                count++;
                index++;
            }
        }

        return count;
    }

    private static void AssertNoLoneLineEndings(ReadOnlySpan<byte> bytes)
    {
        for (var index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] == (byte)'\r')
            {
                Assert.IsTrue(index + 1 < bytes.Length && bytes[index + 1] == (byte)'\n');
            }

            if (bytes[index] == (byte)'\n')
            {
                Assert.IsTrue(index > 0 && bytes[index - 1] == (byte)'\r');
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                directory.FullName,
                "docs",
                "plans",
                "2026-08-27-oni-mod-pipeline-implementation.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
