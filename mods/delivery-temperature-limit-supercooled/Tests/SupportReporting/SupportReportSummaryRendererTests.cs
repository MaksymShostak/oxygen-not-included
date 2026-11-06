namespace DeliveryTemperatureLimit.Tests.SupportReporting;
public sealed class SupportReportSummaryRendererTests
{
    [Test]
    public async Task Render_WhenStandardReportIsAvailable_EmitsOnlyTheAllowlistedCompactFacts()
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
            "- Local report file: `temperature-limit-support-20260831T070809123Z-00112233.json` (not attached automatically)\n" +
            "- ONI build / branch: `744825` / `public`\n" +
            "- Temperature Limit version: `1.3.0`\n" +
            "- Platform: `WindowsPlayer`\n" +
            "- DLCs: `EXPANSION1_ID`, `BASE_GAME`\n" +
            "- External mod integrations:\n" +
            "  - Fast Track: match `not-matched`; capabilities: " +
            "`world-inventory-temperature-publication=not-applicable`\n" +
            "- Player.log excerpt: not collected";
        await Assert.That(summary).IsEqualTo(expected);
        await Assert.That(summary).DoesNotContain("SECRET ACTIVE MOD TITLE");
        await Assert.That(summary).DoesNotContain("SECRET RAW DIAGNOSTIC");
    }

    [Test]
    public async Task Render_WhenExtendedLogIsUnavailable_ReportsRequestedLogWithoutClaimingInclusion()
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

        await Assert.That(summary).EndsWith("- Player.log excerpt: requested but unavailable");
    }

    [Test]
    public async Task Render_WhenLogWasCollected_ExplainsLocalInclusionWithoutClaimingAnAttachment()
    {
        var log = SupportPlayerLogSnapshot.Available("unity-console-log-path", 100, 100,
            false, Array.Empty<string>(), "PRIVATE LOG CONTENT");
        string summary = SupportReportSummaryRenderer.Render(
            CreateDocument(log, "Example mod", "Example diagnostic"), "report.json");

        await Assert.That(summary).Contains("Local report file: `report.json` (not attached automatically)");
        await Assert.That(summary).EndsWith("Player.log excerpt: included in the local JSON report. " +
            "Attach that report to share the log; no files are attached automatically.");
        await Assert.That(summary).DoesNotContain("PRIVATE LOG CONTENT");
    }

    [Test]
    public async Task Render_WhenDlcDiscoveryIsUnavailable_ReportsUnavailableInsteadOfNone()
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

        await Assert.That(summary).Contains("- DLCs: unavailable\n");
        await Assert.That(summary).DoesNotContain("- DLCs: none");
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
        var fastTrack = new SupportExternalModIntegrationSnapshot(
            "fast-track",
            "Fast Track",
            new[] { "exclusive-runtime-authority" },
            "not-matched",
            SupportReportFact.Unavailable("Fast Track is not loaded."),
            SupportReportFact.Unavailable("Fast Track is not loaded."),
            SupportReportFact.Unavailable("Fast Track is not loaded."),
            SupportReportFact.Unavailable("Fast Track is not loaded."),
            new[]
            {
                new SupportExternalModCapabilitySnapshot(
                    "world-inventory-temperature-publication",
                    "does-not-own",
                    "not-evaluated",
                    "not-applicable",
                    diagnosticCode: null,
                    diagnosticMessage: null)
            },
            Array.Empty<SupportDiagnosticSnapshot>());
        SupportRuntimeSnapshot runtime = SupportRuntimeSnapshot.Available(
            "Installed",
            new[] { "GameSessionLifecycle" },
            statusCompatibilityDiagnostic: null,
            new[] { fastTrack });
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
