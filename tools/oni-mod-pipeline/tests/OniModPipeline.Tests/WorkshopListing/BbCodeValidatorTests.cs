using MaksymShostak.OniModPipeline.WorkshopListing;
using System.Text;

namespace MaksymShostak.OniModPipeline.Tests.WorkshopListing;
public sealed class BbCodeValidatorTests
{
    [Test]
    public async Task Validate_WhenSupportedTagsAreProperlyNested_AcceptsDocument()
    {
        const string text =
            "[h1]Heading[/h1]\n[list]\n[*] [b]Bold[/b]\n[*] [url=https://example.invalid]Link[/url]\n[/list]\n---\n[sd] QooLiO";
        var validator = new BbCodeValidator();

        var reasons = validator.Validate(text);

        await Assert.That(reasons.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("[b]missing close")]
    [Arguments("[b][i]crossed[/b][/i]")]
    [Arguments("[/quote]")]
    [Arguments("[*] outside list")]
    public async Task Validate_WhenSupportedStructureIsInvalid_RejectsDocument(string text)
    {
        var reasons = new BbCodeValidator().Validate(text);

        await Assert.That(reasons.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Validate_WhenMarkdownLinkIsPresent_RejectsDocument()
    {
        var reasons = new BbCodeValidator().Validate(
            "Read [the documentation](https://example.invalid).\n");

        await Assert.That(reasons.Any(reason =>
            reason.Contains("Markdown", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    [Arguments("ftp://example.invalid")]
    [Arguments("steam://open")]
    [Arguments("javascript:alert(1)")]
    [Arguments("relative/path")]
    public async Task Validate_WhenUrlSchemeIsUnsupported_RejectsDocument(string url)
    {
        var reasons = new BbCodeValidator().Validate($"[url={url}]Link[/url]");

        await Assert.That(reasons.Any(reason =>
            reason.Contains("http", StringComparison.OrdinalIgnoreCase))).IsTrue();
    }

    [Test]
    [Arguments("http://example.invalid")]
    [Arguments("https://example.invalid/path?q=1")]
    public async Task Validate_WhenUrlSchemeIsSupported_AcceptsDocument(string url)
    {
        var reasons = new BbCodeValidator().Validate($"[url={url}]Link[/url]");

        await Assert.That(reasons.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("alpha\rbravo\n")]
    [Arguments("alpha\r\nbravo\n")]
    [Arguments("alpha")]
    [Arguments("alpha\n\n")]
    public async Task ValidateText_WhenSourceRepresentationIsNotLfWithOneFinalLf_ReturnsOnip1006(
        string source)
    {
        var result = new WorkshopListingValidator().ValidateText(
            "workshop-listing.description",
            Encoding.UTF8.GetBytes(source),
            8000);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Diagnostics[0].Id).IsEqualTo("ONIP1006");
    }

    [Test]
    [Arguments("")]
    [Arguments(" \t\n")]
    public async Task ValidateText_WhenNotesAreEmpty_ReturnsOnip1006(string source)
    {
        var result = new WorkshopListingValidator().ValidateText(
            "workshop-listing.change-notes",
            Encoding.UTF8.GetBytes(source),
            8000);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Diagnostics.All(diagnostic => diagnostic.Id == "ONIP1006")).IsTrue();
    }

    [Test]
    [Arguments("TODO")]
    [Arguments("tbd")]
    [Arguments(" ChangeMe ")]
    [Arguments("ONI_MOD_PIPELINE_CHANGE_NOTES_REQUIRED")]
    public async Task ValidateText_WhenWholeFileIsReservedPlaceholder_ReturnsOnip1006(
        string placeholder)
    {
        var result = new WorkshopListingValidator().ValidateText(
            "workshop-listing.change-notes",
            Encoding.UTF8.GetBytes($"{placeholder}\n"),
            8000);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Diagnostics.Any(diagnostic =>
            diagnostic.Evidence.Contains("placeholder", StringComparison.OrdinalIgnoreCase))).IsTrue();
    }

    [Test]
    public async Task ValidateText_WhenRenderedArtifactIs8000Utf8Bytes_AcceptsDocument()
    {
        var source = new string('a', 7998) + "\n";

        var result = new WorkshopListingValidator().ValidateText(
            "workshop-listing.description",
            Encoding.UTF8.GetBytes(source),
            8000);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value?.Report.Utf8ByteCount).IsEqualTo(8000L);
    }

    [Test]
    public async Task ValidateText_WhenRenderedArtifactIs8001Utf8Bytes_ReturnsOnip1006()
    {
        var source = new string('a', 7999) + "\n";

        var result = new WorkshopListingValidator().ValidateText(
            "workshop-listing.description",
            Encoding.UTF8.GetBytes(source),
            8000);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Diagnostics.Single().Id).IsEqualTo("ONIP1006");
        await Assert.That(result.Diagnostics.Single().Evidence).Contains("8,001");
    }

    [Test]
    public async Task ValidateText_WhenBbCodeIsInvalid_ReturnsOnip1006()
    {
        var result = new WorkshopListingValidator().ValidateText(
            "workshop-listing.description",
            Encoding.UTF8.GetBytes("[b]broken\n"),
            8000);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Diagnostics.All(diagnostic => diagnostic.Id == "ONIP1006")).IsTrue();
    }
}
