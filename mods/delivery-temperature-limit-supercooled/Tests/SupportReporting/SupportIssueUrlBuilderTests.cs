namespace DeliveryTemperatureLimit.Tests.SupportReporting;

[TestClass]
public sealed class SupportIssueUrlBuilderTests
{
    [TestMethod]
    public void Create_WhenSummaryContainsReservedAndUnicodeText_PercentEncodesExactFixedQuery()
    {
        const string summary = "build=744825 & branch=#public\r\nПривіт";

        SupportIssueUrl result = SupportIssueUrlBuilder.Create(summary);

        const string expected =
            "https://github.com/MaksymShostak/oxygen-not-included/issues/new" +
            "?template=temperature-limit-bug.yml" +
            "&diagnostics=build%3D744825%20%26%20branch%3D%23public" +
            "%0D%0A%D0%9F%D1%80%D0%B8%D0%B2%D1%96%D1%82";
        Assert.AreEqual(expected, result.Value);
        Assert.IsFalse(result.SummaryWasShortened);
    }

    [TestMethod]
    public void Create_WhenSummaryAttemptsUrlInjection_KeepsFixedHostAndPath()
    {
        const string summary =
            "&template=https://evil.example/#fragment\r\nnext=value";

        SupportIssueUrl result = SupportIssueUrlBuilder.Create(summary);
        var uri = new Uri(result.Value, UriKind.Absolute);

        Assert.AreEqual(Uri.UriSchemeHttps, uri.Scheme);
        Assert.AreEqual("github.com", uri.Host);
        Assert.AreEqual(
            "/MaksymShostak/oxygen-not-included/issues/new",
            uri.AbsolutePath);
        Assert.DoesNotContain("evil.example/#fragment", result.Value);
        Assert.Contains(
            "diagnostics=%26template%3Dhttps%3A%2F%2Fevil.example%2F%23fragment%0D%0A",
            result.Value);
    }

    [TestMethod]
    public void Create_WhenEncodedSummaryWouldExceedLimit_ShortensDeterministicallyWithMarker()
    {
        string summary = new('&', 5_000);

        SupportIssueUrl first = SupportIssueUrlBuilder.Create(summary);
        SupportIssueUrl second = SupportIssueUrlBuilder.Create(summary);

        Assert.IsTrue(first.SummaryWasShortened);
        Assert.IsLessThanOrEqualTo(
            SupportReportLimits.MaximumIssueUrlCharacters,
            first.Value.Length);
        Assert.AreEqual(first.Value, second.Value);
        const string queryPrefix =
            "?template=temperature-limit-bug.yml&diagnostics=";
        string encodedSummary = new Uri(first.Value).Query
            .Substring(queryPrefix.Length);
        string decodedSummary = Uri.UnescapeDataString(encodedSummary);
        Assert.EndsWith(
            "… [summary shortened; see attached report]",
            decodedSummary);
        string retainedPrefix = decodedSummary.Substring(
            0,
            decodedSummary.Length -
                "… [summary shortened; see attached report]".Length);
        Assert.IsTrue(retainedPrefix.All(character => character == '&'));
    }

    [TestMethod]
    public void Create_WhenSummaryIsNull_RejectsMissingDiagnostics()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            SupportIssueUrlBuilder.Create(null!));
    }
}
