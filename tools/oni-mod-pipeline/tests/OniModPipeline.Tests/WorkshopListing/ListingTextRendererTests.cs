using MaksymShostak.OniModPipeline.WorkshopListing;
using MaksymShostak.OniModPipeline.Tests.Fixtures;
using System.Security.Cryptography;
using System.Text;

namespace MaksymShostak.OniModPipeline.Tests.WorkshopListing;
public sealed class ListingTextRendererTests
{
    [Test]
    [Arguments("lf")]
    [Arguments("crlf")]
    [Arguments("cr")]
    [Arguments("mixed")]
    public async Task Render_WhenInputUsesAnyLineBreakStyle_PreservesOneLogicalDocument(
        string variant)
    {
        var expected = await ReadStructuralFixture();
        var input = CreateLineEndingVariant(expected, variant);
        var renderer = new ListingTextRenderer();

        var rendered = renderer.Render(input);

        await Assert.That(rendered.Bytes).IsEquivalentTo(Encoding.UTF8.GetBytes(expected.Replace("\n", "\r\n", StringComparison.Ordinal)), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(rendered.Report.Encoding).IsEqualTo("utf-8");
        await Assert.That(rendered.Report.HasBom).IsFalse();
        await Assert.That(rendered.Report.LineEndings).IsEqualTo("crlf");
        await Assert.That(rendered.Report.BlankLineCount).IsEqualTo(2);
    }

    [Test]
    public async Task Render_WhenCalledTwice_IsLogicallyIdempotent()
    {
        var renderer = new ListingTextRenderer();
        var first = renderer.Render((await ReadStructuralFixture()));

        var second = renderer.Render(Encoding.UTF8.GetString(first.Bytes));

        await Assert.That(second.Bytes).IsEquivalentTo(first.Bytes, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(second.Report.LogicalContentSha256).IsEqualTo(first.Report.LogicalContentSha256);
        await Assert.That(second.Report.ArtifactSha256).IsEqualTo(first.Report.ArtifactSha256);
    }

    [Test]
    public async Task Render_WhenArtifactIsWritten_UsesNoBomAndOnlyCrLfWithOneFinalCrLf()
    {
        var renderer = new ListingTextRenderer();

        var rendered = renderer.Render("alpha\r\n\r\nbeta\r\r\n");

        await Assert.That(rendered.Bytes.AsSpan().StartsWith(
            new byte[] { 0xEF, 0xBB, 0xBF })).IsFalse();
        await Assert.That(rendered.Bytes.AsSpan().EndsWith("\r\n"u8)).IsTrue();
        await Assert.That(rendered.Bytes.AsSpan().EndsWith("\r\n\r\n"u8)).IsFalse();
        await Assert.That(CountCrLfPairs(rendered.Bytes)).IsEqualTo(3);
        await AssertNoLoneLineEndings(rendered.Bytes);
        await Assert.That(rendered.Report.LogicalLineCount).IsEqualTo(4);
        await Assert.That(rendered.Report.LineBreakCount).IsEqualTo(3);
        await Assert.That(rendered.Report.BlankLineCount).IsEqualTo(1);
    }


    [Test]
    public async Task FindRepositoryRoot_WhenDocumentationIsAbsent_UsesToolingOwnership()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var tooling = Directory.CreateDirectory(Path.Combine(
            temporaryDirectory.Path, "tools", "oni-mod-pipeline"));
        File.WriteAllText(Path.Combine(tooling.FullName, "OniModPipeline.slnx"), "<Solution />");
        var start = Directory.CreateDirectory(Path.Combine(tooling.FullName, "tests", "bin"));

        await Assert.That(Directory.Exists(Path.Combine(temporaryDirectory.Path, "docs"))).IsFalse();
        await Assert.That(FindRepositoryRoot(start)).IsEqualTo(temporaryDirectory.Path);
    }

    private static async Task<string> ReadStructuralFixture()
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
        await Assert.That(text.EndsWith('\n')).IsTrue();
        await Assert.That(text.Contains('\r')).IsFalse();
        await Assert.That(text.EndsWith("\n\n", StringComparison.Ordinal)).IsFalse();
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

    private static async Task AssertNoLoneLineEndings(byte[] bytes)
    {
        for (var index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] == (byte)'\r')
            {
                await Assert.That(index + 1 < bytes.Length && bytes[index + 1] == (byte)'\n').IsTrue();
            }

            if (bytes[index] == (byte)'\n')
            {
                await Assert.That(index > 0 && bytes[index - 1] == (byte)'\r').IsTrue();
            }
        }
    }

    private static string FindRepositoryRoot(DirectoryInfo? start = null)
    {
        var directory = start ?? new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                directory.FullName,
                "tools",
                "oni-mod-pipeline",
                "OniModPipeline.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
