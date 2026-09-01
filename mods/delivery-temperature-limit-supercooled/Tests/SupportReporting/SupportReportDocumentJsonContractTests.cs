using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DeliveryTemperatureLimit.Tests.SupportReporting;

[TestClass]
public sealed class SupportReportDocumentJsonContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [TestMethod]
    public void Serialize_WhenNoDeclaredIntegrationOutcomesExist_UsesSchemaVersionTwoEmptyGenericCollection()
    {
        SupportReportDocument document = CreateDocument(
            Array.Empty<SupportExternalModIntegrationSnapshot>());

        JsonObject root = SerializeToObject(document);
        JsonObject runtime = root["runtime"]!.AsObject();

        Assert.AreEqual(2, root["schemaVersion"]!.GetValue<int>());
        Assert.HasCount(0, runtime["externalModIntegrations"]!.AsArray());
        Assert.IsFalse(
            ContainsPropertyNamed(root, "fastTrack"),
            "Schema version 2 must not retain the singular runtime.fastTrack shape.");
    }

    [TestMethod]
    public void Serialize_WhenOneDeclaredIntegrationOutcomeExists_ProjectsOnlyAllowlistedGenericFacts()
    {
        var fastTrack = CreateIntegration(
            "fast-track",
            "Fast Track",
            "matched",
            new[]
            {
                new SupportExternalModCapabilitySnapshot(
                    "world-inventory-temperature-publication",
                    "owns-compatible",
                    "compatible",
                    "selected",
                    diagnosticCode: null,
                    diagnosticMessage: null)
            });
        SupportReportDocument document = CreateDocument(new[] { fastTrack });

        JsonObject root = SerializeToObject(document);
        JsonObject integration = root["runtime"]!
            ["externalModIntegrations"]!
            .AsArray()[0]!
            .AsObject();

        Assert.AreEqual("fast-track", integration["integrationId"]!.GetValue<string>());
        Assert.AreEqual("Fast Track", integration["displayName"]!.GetValue<string>());
        CollectionAssert.AreEqual(
            new[] { "exclusive-runtime-authority" },
            integration["categories"]!.AsArray()
                .Select(category => category!.GetValue<string>())
                .ToArray());
        Assert.AreEqual("matched", integration["matchState"]!.GetValue<string>());
        Assert.AreEqual(
            "FastTrack, Version=0.18.5.0",
            integration["assemblyIdentity"]!["value"]!.GetValue<string>());
        Assert.HasCount(1, integration["capabilities"]!.AsArray());
        Assert.HasCount(0, integration["diagnostics"]!.AsArray());
        Assert.IsFalse(ContainsPropertyNamed(root, "fastTrack"));
    }

    [TestMethod]
    public void Serialize_WhenMultipleDeclaredIntegrationOutcomesExist_PreservesCatalogAndCapabilityOrder()
    {
        var first = CreateIntegration(
            "first-integration",
            "First Integration",
            "matched",
            new[]
            {
                CreateCapability("first-capability", "ready"),
                CreateCapability("second-capability", "selected")
            });
        var second = CreateIntegration(
            "second-integration",
            "Second Integration",
            "not-matched",
            new[]
            {
                CreateCapability("third-capability", "not-applicable")
            });
        SupportReportDocument document = CreateDocument(
            new[] { first, second });

        JsonArray integrations = SerializeToObject(document)["runtime"]!
            ["externalModIntegrations"]!
            .AsArray();

        CollectionAssert.AreEqual(
            new[] { "first-integration", "second-integration" },
            integrations
                .Select(integration =>
                    integration!["integrationId"]!.GetValue<string>())
                .ToArray());
        CollectionAssert.AreEqual(
            new[] { "first-capability", "second-capability" },
            integrations[0]!["capabilities"]!
                .AsArray()
                .Select(capability =>
                    capability!["capabilityId"]!.GetValue<string>())
                .ToArray());
    }

    [TestMethod]
    public void IntegrationSnapshot_WhenCapabilityCountExceedsSchemaLimit_RejectsUnboundedEvidence()
    {
        SupportExternalModCapabilitySnapshot[] capabilities = Enumerable
            .Range(
                0,
                SupportReportLimits
                    .MaximumExternalModCapabilitiesPerIntegration + 1)
            .Select(index => CreateCapability(
                $"capability-{index:D3}",
                "ready"))
            .ToArray();

        Assert.ThrowsExactly<ArgumentException>(() => CreateIntegration(
            "bounded-integration",
            "Bounded Integration",
            "matched",
            capabilities));
    }

    private static SupportExternalModCapabilitySnapshot CreateCapability(
        string capabilityId,
        string disposition) =>
        new(
            capabilityId,
            "does-not-own",
            "not-evaluated",
            disposition,
            diagnosticCode: null,
            diagnosticMessage: null);

    private static SupportExternalModIntegrationSnapshot CreateIntegration(
        string integrationId,
        string displayName,
        string matchState,
        IEnumerable<SupportExternalModCapabilitySnapshot> capabilities) =>
        new(
            integrationId,
            displayName,
            new[] { "exclusive-runtime-authority" },
            matchState,
            SupportReportFact.Available("FastTrack, Version=0.18.5.0"),
            SupportReportFact.Available("0.18.5.0"),
            SupportReportFact.Available("0.18.5.0"),
            SupportReportFact.Available(
                new string('A', 64)),
            capabilities,
            Array.Empty<SupportDiagnosticSnapshot>());

    private static SupportReportDocument CreateDocument(
        IEnumerable<SupportExternalModIntegrationSnapshot>
            externalModIntegrations)
    {
        DateTimeOffset generatedAtUtc =
            new(2026, 9, 1, 8, 9, 10, TimeSpan.Zero);
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
            SupportRuntimeSnapshot.Available(
                "Installed",
                new[] { "game-session-lifecycle" },
                statusCompatibilityDiagnostic: null,
                externalModIntegrations),
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

    private static JsonObject SerializeToObject(
        SupportReportDocument document) =>
        JsonSerializer.SerializeToNode(document, JsonOptions)!
            .AsObject();

    private static bool ContainsPropertyNamed(
        JsonNode? node,
        string propertyName)
    {
        if (node is JsonObject jsonObject)
        {
            foreach ((string key, JsonNode? value) in jsonObject)
            {
                if (string.Equals(key, propertyName, StringComparison.Ordinal) ||
                    ContainsPropertyNamed(value, propertyName))
                {
                    return true;
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (JsonNode? item in jsonArray)
            {
                if (ContainsPropertyNamed(item, propertyName))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
