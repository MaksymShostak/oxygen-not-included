namespace DeliveryTemperatureLimit.Tests.SupportReporting;

[TestClass]
public sealed class SupportJsonReportSizeLimiterTests
{
    [TestMethod]
    public void SerializeWithinLimit_WhenGenericIntegrationEvidenceFits_PreservesTheCompleteDocument()
    {
        SupportReportDocument document = CreateStandardDocument();

        SupportJsonReportSerialization serialization =
            SupportJsonReportSizeLimiter.SerializeWithinLimit(
                document,
                maximumByteCount: 6,
                candidate => candidate.Runtime.ExternalModIntegrations.Count == 1
                    ? "fixed"
                    : string.Empty);

        Assert.AreSame(document, serialization.Document);
        Assert.AreEqual("fixed", serialization.Json);
        Assert.AreEqual(5, serialization.Utf8ByteCount);
        Assert.HasCount(
            1,
            serialization.Document.Runtime.ExternalModIntegrations);
    }

    [TestMethod]
    public void SerializeWithinLimit_WhenGenericIntegrationEvidenceReachesLimit_RejectsInsteadOfDroppingEvidence()
    {
        SupportReportDocument document = CreateStandardDocument();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            SupportJsonReportSizeLimiter.SerializeWithinLimit(
                document,
                maximumByteCount: 5,
                candidate => candidate.Runtime.ExternalModIntegrations.Count == 1
                    ? "fixed"
                    : string.Empty));
    }

    private static SupportReportDocument CreateStandardDocument()
    {
        DateTimeOffset generatedAtUtc =
            new(2026, 9, 1, 8, 9, 10, TimeSpan.Zero);
        var integration = new SupportExternalModIntegrationSnapshot(
            "example-integration",
            "Example Integration",
            new[] { "additive-interoperability" },
            "matched",
            SupportReportFact.Unavailable(
                "This integration does not inspect an assembly identity."),
            SupportReportFact.Unavailable(
                "This integration does not inspect an assembly version."),
            SupportReportFact.Unavailable(
                "This integration does not inspect a file version."),
            SupportReportFact.Unavailable(
                "This integration does not inspect an assembly digest."),
            new[]
            {
                new SupportExternalModCapabilitySnapshot(
                    "settings-transfer",
                    "does-not-own",
                    "compatible",
                    "ready",
                    diagnosticCode: null,
                    diagnosticMessage: null)
            },
            Array.Empty<SupportDiagnosticSnapshot>());
        SupportRuntimeSnapshot runtime = SupportRuntimeSnapshot.Available(
            "Installed",
            new[] { "game-session-lifecycle" },
            statusCompatibilityDiagnostic: null,
            new[] { integration });

        return new SupportReportDocument(
            "00112233445566778899aabbccddeeff",
            generatedAtUtc,
            SupportReportKind.Standard,
            new SupportReportGameSnapshot(
                SupportReportFact.Available("744825"),
                SupportReportFact.Available("public"),
                SupportReportFact.Available("U55-744825"),
                SupportReportFact.Available("6000.0.42f1"),
                SupportReportFact.Available("WindowsPlayer"),
                SupportReportFact.Available("x64"),
                SupportReportFact.Available("en-US"),
                SupportActiveDlcSnapshot.Available(Array.Empty<string>())),
            new SupportReportTemperatureLimitSnapshot(
                SupportReportFact.Available(
                    "llunak.DeliveryTemperatureLimit"),
                SupportReportFact.Available(
                    "Delivery Temperature Limit (Supercooled)"),
                SupportReportFact.Available("1.3.0"),
                SupportReportFact.Available("1.3.0.0"),
                checkTemperatureForStatusItems: true,
                underConstructionLimit: false,
                temperatureUnit: "Celsius",
                maxConstructionTemperature: 45,
                minConstructionTemperature: -50),
            runtime,
            Array.Empty<SupportActiveModSnapshot>(),
            omittedActiveModCount: 0,
            Array.Empty<SupportDiagnosticSnapshot>(),
            omittedDistinctDiagnosticCount: 0,
            playerLog: null,
            new SupportGenerationSnapshot(
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                issueSummaryWasShortened: false),
            new SupportPrivacySnapshot(
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>()));
    }
}
