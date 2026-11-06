namespace DeliveryTemperatureLimit.Tests.SupportReporting;
public sealed class SupportIssueUrlBuilderTests
{
    [Test]
    public async Task Create_WhenSummaryContainsReservedAndUnicodeText_PercentEncodesExactFixedQuery()
    {
        const string summary = "build=744825 & branch=#public\r\nПривіт";

        SupportIssueUrl result = SupportIssueUrlBuilder.Create(summary);

        const string expected =
            "https://github.com/MaksymShostak/oxygen-not-included/issues/new" +
            "?template=temperature-limit-bug.yml" +
            "&diagnostics=build%3D744825%20%26%20branch%3D%23public" +
            "%0D%0A%D0%9F%D1%80%D0%B8%D0%B2%D1%96%D1%82";
        await Assert.That(result.Value).IsEqualTo(expected);
        await Assert.That(result.SummaryWasShortened).IsFalse();
    }

    [Test]
    public async Task Create_WhenSummaryAttemptsUrlInjection_KeepsFixedHostAndPath()
    {
        const string summary =
            "&template=https://evil.example/#fragment\r\nnext=value";

        SupportIssueUrl result = SupportIssueUrlBuilder.Create(summary);
        var uri = new Uri(result.Value, UriKind.Absolute);

        await Assert.That(uri.Scheme).IsEqualTo(Uri.UriSchemeHttps);
        await Assert.That(uri.Host).IsEqualTo("github.com");
        await Assert.That(uri.AbsolutePath).IsEqualTo("/MaksymShostak/oxygen-not-included/issues/new");
        await Assert.That(result.Value).DoesNotContain("evil.example/#fragment");
        await Assert.That(result.Value).Contains("diagnostics=%26template%3Dhttps%3A%2F%2Fevil.example%2F%23fragment%0D%0A");
    }

    [Test]
    public async Task Create_WhenEncodedSummaryWouldExceedLimit_ShortensDeterministicallyWithMarker()
    {
        string summary = new('&', 5_000);

        SupportIssueUrl first = SupportIssueUrlBuilder.Create(summary);
        SupportIssueUrl second = SupportIssueUrlBuilder.Create(summary);

        await Assert.That(first.SummaryWasShortened).IsTrue();
        await Assert.That(first.Value.Length).IsLessThanOrEqualTo(SupportReportLimits.MaximumIssueUrlCharacters);
        await Assert.That(second.Value).IsEqualTo(first.Value);
        const string queryPrefix =
            "?template=temperature-limit-bug.yml&diagnostics=";
        string encodedSummary = new Uri(first.Value).Query
            .Substring(queryPrefix.Length);
        string decodedSummary = Uri.UnescapeDataString(encodedSummary);
        await Assert.That(decodedSummary).EndsWith("… [summary shortened; full details are in the local report file]");
        string retainedPrefix = decodedSummary.Substring(
            0,
            decodedSummary.Length -
                "… [summary shortened; full details are in the local report file]".Length);
        await Assert.That(retainedPrefix.All(character => character == '&')).IsTrue();
    }

    [Test]
    public async Task Create_WhenSummaryIsNull_RejectsMissingDiagnostics()
    {
        await Assert.That(() =>
            SupportIssueUrlBuilder.Create(null!)).ThrowsExactly<ArgumentNullException>();
    }
}
