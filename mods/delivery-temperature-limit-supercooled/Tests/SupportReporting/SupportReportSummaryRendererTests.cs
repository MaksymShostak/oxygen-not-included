namespace DeliveryTemperatureLimit.Tests.SupportReporting;

[TestClass]
public sealed class SupportReportSummaryRendererTests
{
    [TestMethod]
    public void Render_WhenStandardReportIsAvailable_EmitsOnlyTheAllowlistedCompactFacts()
    {
        SupportReportDocument document = CreateDocument(
            playerLog: null,
            activeModTitle: "SECRET ACTIVE MOD TITLE",
            diagnosticMessage: "SECRET RAW DIAGNOSTIC");

        string summary = SupportReportSummaryRenderer.Render(
            document,
            "temperature-limit-support-20260831T070809123Z-00112233.json");

        const string expected =
            "### Temperature Limit diagnostics\n\n" +
            "- Report ID: `00112233445566778899aabbccddeeff`\n" +
            "- Report file: `temperature-limit-support-20260831T070809123Z-00112233.json`\n" +
            "- ONI build / branch: `744825` / `public`\n" +
            "- Temperature Limit version: `1.3.0`\n" +
            "- Platform: `WindowsPlayer`\n" +
            "- DLCs: `EXPANSION1_ID`, `BASE_GAME`\n" +
            "- FastTrack: `not-loaded`\n" +
            "- Player.log: not included";
        Assert.AreEqual(expected, summary);
        Assert.DoesNotContain("SECRET ACTIVE MOD TITLE", summary);
        Assert.DoesNotContain("SECRET RAW DIAGNOSTIC", summary);
    }

    [TestMethod]
    public void Render_WhenExtendedLogIsUnavailable_ReportsRequestedLogWithoutClaimingInclusion()
    {
        SupportPlayerLogSnapshot playerLog =
            SupportPlayerLogSnapshot.Unavailable(
                "unity-console-log-path",
                "The current log could not be opened.");
        SupportReportDocument document = CreateDocument(
            playerLog,
            activeModTitle: "Example mod",
            diagnosticMessage: "Example diagnostic");

        string summary = SupportReportSummaryRenderer.Render(
            document,
            "temperature-limit-support-20260831T070809123Z-00112233.json");

        Assert.EndsWith(
            "- Player.log: requested but unavailable",
            summary);
    }

    [TestMethod]
    public void Render_WhenDlcDiscoveryIsUnavailable_ReportsUnavailableInsteadOfNone()
    {
        SupportReportDocument document = CreateDocument(
            playerLog: null,
            activeModTitle: "Example mod",
            diagnosticMessage: "Example diagnostic",
            activeDlcs: SupportActiveDlcSnapshot.Unavailable(
                "DLC discovery failed."));

        string summary = SupportReportSummaryRenderer.Render(
            document,
            "temperature-limit-support-20260831T070809123Z-00112233.json");

        Assert.Contains("- DLCs: unavailable\n", summary);
        Assert.DoesNotContain("- DLCs: none", summary);
    }

    private static SupportReportDocument CreateDocument(
        SupportPlayerLogSnapshot? playerLog,
        string activeModTitle,
        string diagnosticMessage,
        SupportActiveDlcSnapshot? activeDlcs = null)
    {
        DateTimeOffset generatedAtUtc =
            new(2026, 8, 31, 7, 8, 9, TimeSpan.Zero);
        var game = new SupportReportGameSnapshot(
            SupportReportFact.Available("744825"),
            SupportReportFact.Available("public"),
            SupportReportFact.Available("U55-744825"),
            SupportReportFact.Available("6000.0.42f1"),
            SupportReportFact.Available("WindowsPlayer"),
            SupportReportFact.Available("x64"),
            SupportReportFact.Available("en-US"),
            activeDlcs ?? SupportActiveDlcSnapshot.Available(
                new[] { "EXPANSION1_ID", "BASE_GAME" }));
        var temperatureLimit = new SupportReportTemperatureLimitSnapshot(
            SupportReportFact.Available("llunak.DeliveryTemperatureLimit"),
            SupportReportFact.Available(
                "Delivery Temperature Limit (Supercooled)"),
            SupportReportFact.Available("1.3.0"),
            SupportReportFact.Available("1.3.0.0"),
            checkTemperatureForStatusItems: true,
            underConstructionLimit: false,
            temperatureUnit: "Celsius",
            maxConstructionTemperature: 45,
            minConstructionTemperature: -50);
        var fastTrack = new SupportFastTrackSnapshot(
            "not-loaded",
            SupportReportFact.Unavailable("FastTrack is not loaded."),
            SupportReportFact.Unavailable("FastTrack is not loaded."),
            SupportReportFact.Unavailable("FastTrack is not loaded."),
            SupportReportFact.Unavailable("FastTrack is not loaded."),
            Array.Empty<SupportFastTrackFeatureSnapshot>());
        SupportRuntimeSnapshot runtime = SupportRuntimeSnapshot.Available(
            "Installed",
            new[] { "GameSessionLifecycle" },
            statusCompatibilityDiagnostic: null,
            fastTrack);
        var activeMod = new SupportActiveModSnapshot(
            0,
            activeModTitle,
            SupportReportFact.Available("example.mod"),
            SupportReportFact.Available("1.0.0"),
            new[] { "Example 1.0.0.0" },
            "local");
        var diagnostic = new SupportDiagnosticSnapshot(
            "DTL-EXAMPLE",
            "warning",
            generatedAtUtc,
            generatedAtUtc,
            1,
            diagnosticMessage,
            exceptionType: null,
            exceptionMessage: null);
        var generation = new SupportGenerationSnapshot(
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            issueSummaryWasShortened: false);
        var privacy = new SupportPrivacySnapshot(
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());

        return new SupportReportDocument(
            "00112233445566778899aabbccddeeff",
            generatedAtUtc,
            playerLog == null
                ? SupportReportKind.Standard
                : SupportReportKind.ExtendedPlayerLog,
            game,
            temperatureLimit,
            runtime,
            new[] { activeMod },
            0,
            new[] { diagnostic },
            0,
            playerLog,
            generation,
            privacy);
    }
}
