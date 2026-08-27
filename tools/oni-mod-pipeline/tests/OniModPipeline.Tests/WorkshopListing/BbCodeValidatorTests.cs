using MaksymShostak.OniModPipeline.WorkshopListing;
using System.Text;

namespace MaksymShostak.OniModPipeline.Tests.WorkshopListing;

[TestClass]
public sealed class BbCodeValidatorTests
{
    [TestMethod]
    public void Validate_WhenSupportedTagsAreProperlyNested_AcceptsDocument()
    {
        const string text =
            "[h1]Heading[/h1]\n[list]\n[*] [b]Bold[/b]\n[*] [url=https://example.invalid]Link[/url]\n[/list]\n---\n[sd] QooLiO";
        var validator = new BbCodeValidator();

        var reasons = validator.Validate(text);

        Assert.AreEqual(0, reasons.Count);
    }

    [TestMethod]
    [DataRow("[b]missing close")]
    [DataRow("[b][i]crossed[/b][/i]")]
    [DataRow("[/quote]")]
    [DataRow("[*] outside list")]
    public void Validate_WhenSupportedStructureIsInvalid_RejectsDocument(string text)
    {
        var reasons = new BbCodeValidator().Validate(text);

        Assert.IsGreaterThan(0, reasons.Count);
    }

    [TestMethod]
    public void Validate_WhenMarkdownLinkIsPresent_RejectsDocument()
    {
        var reasons = new BbCodeValidator().Validate(
            "Read [the documentation](https://example.invalid).\n");

        Assert.IsTrue(reasons.Any(reason =>
            reason.Contains("Markdown", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("ftp://example.invalid")]
    [DataRow("steam://open")]
    [DataRow("javascript:alert(1)")]
    [DataRow("relative/path")]
    public void Validate_WhenUrlSchemeIsUnsupported_RejectsDocument(string url)
    {
        var reasons = new BbCodeValidator().Validate($"[url={url}]Link[/url]");

        Assert.IsTrue(reasons.Any(reason =>
            reason.Contains("http", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    [DataRow("http://example.invalid")]
    [DataRow("https://example.invalid/path?q=1")]
    public void Validate_WhenUrlSchemeIsSupported_AcceptsDocument(string url)
    {
        var reasons = new BbCodeValidator().Validate($"[url={url}]Link[/url]");

        Assert.AreEqual(0, reasons.Count);
    }

    [TestMethod]
    [DataRow("alpha\rbravo\n")]
    [DataRow("alpha\r\nbravo\n")]
    [DataRow("alpha")]
    [DataRow("alpha\n\n")]
    public void ValidateText_WhenSourceRepresentationIsNotLfWithOneFinalLf_ReturnsOnip1006(
        string source)
    {
        var result = new WorkshopListingValidator().ValidateText(
            "workshop-listing.description",
            Encoding.UTF8.GetBytes(source),
            8000);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("ONIP1006", result.Diagnostics[0].Id);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" \t\n")]
    public void ValidateText_WhenNotesAreEmpty_ReturnsOnip1006(string source)
    {
        var result = new WorkshopListingValidator().ValidateText(
            "workshop-listing.change-notes",
            Encoding.UTF8.GetBytes(source),
            8000);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Diagnostics.All(diagnostic => diagnostic.Id == "ONIP1006"));
    }

    [TestMethod]
    [DataRow("TODO")]
    [DataRow("tbd")]
    [DataRow(" ChangeMe ")]
    [DataRow("ONI_MOD_PIPELINE_CHANGE_NOTES_REQUIRED")]
    public void ValidateText_WhenWholeFileIsReservedPlaceholder_ReturnsOnip1006(
        string placeholder)
    {
        var result = new WorkshopListingValidator().ValidateText(
            "workshop-listing.change-notes",
            Encoding.UTF8.GetBytes($"{placeholder}\n"),
            8000);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Diagnostics.Any(diagnostic =>
            diagnostic.Evidence.Contains("placeholder", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void ValidateText_WhenRenderedArtifactIs8000Utf8Bytes_AcceptsDocument()
    {
        var source = new string('a', 7998) + "\n";

        var result = new WorkshopListingValidator().ValidateText(
            "workshop-listing.description",
            Encoding.UTF8.GetBytes(source),
            8000);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(8000L, result.Value?.Report.Utf8ByteCount);
    }

    [TestMethod]
    public void ValidateText_WhenRenderedArtifactIs8001Utf8Bytes_ReturnsOnip1006()
    {
        var source = new string('a', 7999) + "\n";

        var result = new WorkshopListingValidator().ValidateText(
            "workshop-listing.description",
            Encoding.UTF8.GetBytes(source),
            8000);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("ONIP1006", result.Diagnostics.Single().Id);
        StringAssert.Contains(result.Diagnostics.Single().Evidence, "8,001");
    }

    [TestMethod]
    public void ValidateText_WhenBbCodeIsInvalid_ReturnsOnip1006()
    {
        var result = new WorkshopListingValidator().ValidateText(
            "workshop-listing.description",
            Encoding.UTF8.GetBytes("[b]broken\n"),
            8000);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Diagnostics.All(diagnostic => diagnostic.Id == "ONIP1006"));
    }
}
