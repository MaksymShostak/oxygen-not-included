namespace DeliveryTemperatureLimit.Tests.SupportReporting;

[TestClass]
public sealed class SupportReportDocumentTests
{
    [TestMethod]
    public void Constructor_WhenStandardFactsAreSupplied_PreservesSchemaAndExplicitAvailability()
    {
        SupportReportDocument document = SupportReportDocumentFixture.Create(
            SupportReportKind.Standard,
            playerLog: null);

        Assert.AreEqual(1, document.SchemaVersion);
        Assert.AreEqual("standard", document.ReportKind);
        Assert.AreEqual("available", document.Game.Build.State);
        Assert.AreEqual("744825", document.Game.Build.Value);
        Assert.AreEqual("unavailable", document.Game.GameVersion.State);
        Assert.AreEqual("available", document.Game.ActiveDlcs.State);
        Assert.IsNull(document.PlayerLog);
    }

    [TestMethod]
    public void Constructor_WhenTemperatureUnitIsNonCelsius_PreservesUnitWithDisplayedThresholds()
    {
        SupportReportDocument document = SupportReportDocumentFixture.Create(
            SupportReportKind.Standard,
            playerLog: null,
            temperatureUnit: "Fahrenheit",
            maxConstructionTemperature: 113,
            minConstructionTemperature: -58);

        Assert.AreEqual(
            "Fahrenheit",
            document.TemperatureLimit.TemperatureUnit);
        Assert.AreEqual(
            113,
            document.TemperatureLimit.MaxConstructionTemperature);
        Assert.AreEqual(
            -58,
            document.TemperatureLimit.MinConstructionTemperature);
    }

    [TestMethod]
    public void Constructor_WhenExtendedReportHasNoPlayerLog_RejectsIncompleteDocument()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            SupportReportDocumentFixture.Create(
                SupportReportKind.ExtendedPlayerLog,
                playerLog: null));
    }

    [TestMethod]
    public void Constructor_WhenStandardReportHasPlayerLog_RejectsUnexpectedContent()
    {
        SupportPlayerLogSnapshot playerLog =
            SupportPlayerLogSnapshot.Available(
                "unity-console-log-path",
                originalByteCount: 12,
                includedRawByteCount: 12,
                truncated: false,
                redactedPlaceholders: Array.Empty<string>(),
                content: "sample log");

        Assert.ThrowsExactly<ArgumentException>(() =>
            SupportReportDocumentFixture.Create(
                SupportReportKind.Standard,
                playerLog));
    }

    [TestMethod]
    public void Constructor_WhenCallerMutatesInputLists_PreservesOriginalOrderAndValues()
    {
        var activeDlcIds = new List<string> { "EXPANSION1_ID", "BASE_GAME" };
        var activeMods = new List<SupportActiveModSnapshot>
        {
            SupportReportDocumentFixture.CreateActiveMod(0, "First mod"),
            SupportReportDocumentFixture.CreateActiveMod(1, "Second mod")
        };
        var diagnostics = new List<SupportDiagnosticSnapshot>
        {
            SupportReportDocumentFixture.CreateDiagnostic("DTL-FIRST"),
            SupportReportDocumentFixture.CreateDiagnostic("DTL-SECOND")
        };

        SupportReportDocument document = SupportReportDocumentFixture.Create(
            SupportReportKind.Standard,
            playerLog: null,
            activeDlcIds,
            activeMods,
            diagnostics);

        activeDlcIds[0] = "MUTATED";
        activeMods.Clear();
        diagnostics.Reverse();

        CollectionAssert.AreEqual(
            new[] { "EXPANSION1_ID", "BASE_GAME" },
            document.Game.ActiveDlcs.Ids.ToArray());
        CollectionAssert.AreEqual(
            new[] { "First mod", "Second mod" },
            document.ActiveMods.Select(mod => mod.Title).ToArray());
        CollectionAssert.AreEqual(
            new[] { "DTL-FIRST", "DTL-SECOND" },
            document.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
    }

    [TestMethod]
    public void Constructor_WhenIdentityOrTimestampIsInvalid_RejectsDocument()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            SupportReportDocumentFixture.Create(
                SupportReportKind.Standard,
                playerLog: null,
                reportId: " "));
        Assert.ThrowsExactly<ArgumentException>(() =>
            SupportReportDocumentFixture.Create(
                SupportReportKind.Standard,
                playerLog: null,
                generatedAtUtc: new DateTimeOffset(
                    2026,
                    8,
                    31,
                    10,
                    15,
                    0,
                    TimeSpan.FromHours(3))));
    }

    [TestMethod]
    public void ReportKind_DefinesOnlyTheTwoSchemaVersionOneModes()
    {
        CollectionAssert.AreEqual(
            new[] { "Standard", "ExtendedPlayerLog" },
            Enum.GetNames<SupportReportKind>());
        CollectionAssert.AreEqual(
            new[] { 0, 1 },
            Enum.GetValues<SupportReportKind>()
                .Select(value => (int)value)
                .ToArray());
    }

    [TestMethod]
    public void UnavailableFact_WhenReasonIsBlank_RejectsAmbiguousAbsence()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            SupportReportFact.Unavailable(" "));
    }

    [TestMethod]
    public void WithIssueSummaryWasShortened_WhenUrlWasBounded_ReturnsUpdatedImmutableDocument()
    {
        SupportReportDocument original = SupportReportDocumentFixture.Create(
            SupportReportKind.Standard,
            playerLog: null);

        SupportReportDocument updated =
            original.WithIssueSummaryWasShortened();

        Assert.IsFalse(original.Generation.IssueSummaryWasShortened);
        Assert.IsTrue(updated.Generation.IssueSummaryWasShortened);
        Assert.AreEqual(original.ReportId, updated.ReportId);
        Assert.AreSame(original.Game, updated.Game);
        Assert.AreSame(original.TemperatureLimit, updated.TemperatureLimit);
        Assert.AreSame(original.Runtime, updated.Runtime);
        CollectionAssert.AreEqual(
            original.ActiveMods.ToArray(),
            updated.ActiveMods.ToArray());
        CollectionAssert.AreEqual(
            original.Diagnostics.ToArray(),
            updated.Diagnostics.ToArray());
    }

    [TestMethod]
    public void SerializeWithinLimit_WhenExtendedReportIsTooLarge_KeepsNewestLogContentAndDisclosesShortening()
    {
        SupportPlayerLogSnapshot playerLog =
            SupportPlayerLogSnapshot.Available(
                "unity-console-log-path",
                originalByteCount: 10,
                includedRawByteCount: 10,
                truncated: false,
                redactedPlaceholders: Array.Empty<string>(),
                content: "abcdefghij");
        SupportReportDocument original =
            SupportReportDocumentFixture.Create(
                SupportReportKind.ExtendedPlayerLog,
                playerLog);

        SupportJsonReportSerialization serialization =
            SupportJsonReportSizeLimiter.SerializeWithinLimit(
                original,
                maximumByteCount: 5,
                candidate => candidate.PlayerLog!.Content!);

        Assert.AreEqual("ghij", serialization.Json);
        Assert.AreEqual(4, serialization.Utf8ByteCount);
        Assert.IsTrue(serialization.Utf8ByteCount < 5);
        Assert.AreEqual("abcdefghij", original.PlayerLog!.Content);
        Assert.IsFalse(original.PlayerLog.Truncated);
        Assert.AreEqual("ghij", serialization.Document.PlayerLog!.Content);
        Assert.IsTrue(serialization.Document.PlayerLog.Truncated);
        CollectionAssert.Contains(
            serialization.Document.Generation.Warnings.ToArray(),
            "Player.log content was shortened further to keep the complete " +
                "report below 12 MiB.");
        Assert.AreSame(original.Game, serialization.Document.Game);
        Assert.AreSame(
            original.TemperatureLimit,
            serialization.Document.TemperatureLimit);
        Assert.AreSame(original.Runtime, serialization.Document.Runtime);
    }

    [TestMethod]
    public void SerializeWithinLimit_WhenDocumentAlreadyFits_ReturnsItUnchanged()
    {
        SupportPlayerLogSnapshot playerLog =
            SupportPlayerLogSnapshot.Available(
                "unity-console-log-path",
                originalByteCount: 3,
                includedRawByteCount: 3,
                truncated: false,
                redactedPlaceholders: Array.Empty<string>(),
                content: "abc");
        SupportReportDocument original =
            SupportReportDocumentFixture.Create(
                SupportReportKind.ExtendedPlayerLog,
                playerLog);

        SupportJsonReportSerialization serialization =
            SupportJsonReportSizeLimiter.SerializeWithinLimit(
                original,
                maximumByteCount: 4,
                candidate => candidate.PlayerLog!.Content!);

        Assert.AreSame(original, serialization.Document);
        Assert.AreEqual("abc", serialization.Json);
        Assert.AreEqual(3, serialization.Utf8ByteCount);
        Assert.IsFalse(serialization.Document.PlayerLog!.Truncated);
        Assert.AreEqual(
            0,
            serialization.Document.Generation.Warnings.Count);
    }

    [TestMethod]
    public void SerializeWithinLimit_WhenNonLogDocumentIsAtLimit_RejectsReport()
    {
        SupportPlayerLogSnapshot playerLog =
            SupportPlayerLogSnapshot.Available(
                "unity-console-log-path",
                originalByteCount: 3,
                includedRawByteCount: 3,
                truncated: false,
                redactedPlaceholders: Array.Empty<string>(),
                content: "abc");
        SupportReportDocument document =
            SupportReportDocumentFixture.Create(
                SupportReportKind.ExtendedPlayerLog,
                playerLog);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            SupportJsonReportSizeLimiter.SerializeWithinLimit(
                document,
                maximumByteCount: 5,
                candidate =>
                    "fixed" + (candidate.PlayerLog!.Content ?? string.Empty)));
    }

    private static class SupportReportDocumentFixture
    {
        private static readonly DateTimeOffset GeneratedAtUtc =
            new(2026, 8, 31, 7, 8, 9, TimeSpan.Zero);

        internal static SupportReportDocument Create(
            SupportReportKind kind,
            SupportPlayerLogSnapshot? playerLog,
            IEnumerable<string>? activeDlcIds = null,
            IEnumerable<SupportActiveModSnapshot>? activeMods = null,
            IEnumerable<SupportDiagnosticSnapshot>? diagnostics = null,
            string reportId = "00112233445566778899aabbccddeeff",
            DateTimeOffset? generatedAtUtc = null,
            string temperatureUnit = "Celsius",
            int maxConstructionTemperature = 45,
            int minConstructionTemperature = -50)
        {
            var game = new SupportReportGameSnapshot(
                SupportReportFact.Available("744825"),
                SupportReportFact.Available("public"),
                SupportReportFact.Unavailable("Game version API was unavailable."),
                SupportReportFact.Available("6000.0.42f1"),
                SupportReportFact.Available("WindowsPlayer"),
                SupportReportFact.Available("x64"),
                SupportReportFact.Available("en-US"),
                SupportActiveDlcSnapshot.Available(
                    activeDlcIds ??
                        new[] { "EXPANSION1_ID", "BASE_GAME" }));
            var temperatureLimit = new SupportReportTemperatureLimitSnapshot(
                SupportReportFact.Available("llunak.DeliveryTemperatureLimit"),
                SupportReportFact.Available(
                    "Delivery Temperature Limit (Supercooled)"),
                SupportReportFact.Available("1.3.0"),
                SupportReportFact.Available("1.3.0.0"),
                checkTemperatureForStatusItems: true,
                underConstructionLimit: false,
                temperatureUnit,
                maxConstructionTemperature,
                minConstructionTemperature);
            var fastTrack = new SupportFastTrackSnapshot(
                "not-loaded",
                SupportReportFact.Unavailable("FastTrack is not loaded."),
                SupportReportFact.Unavailable("FastTrack is not loaded."),
                SupportReportFact.Unavailable("FastTrack is not loaded."),
                SupportReportFact.Unavailable("FastTrack is not loaded."),
                new[]
                {
                    new SupportFastTrackFeatureSnapshot(
                        "WorldInventory",
                        "mod-not-loaded",
                        failureCode: null,
                        failureMessage: null)
                });
            SupportRuntimeSnapshot runtime = SupportRuntimeSnapshot.Available(
                "Installed",
                new[]
                {
                    "GameSessionLifecycle",
                    "WorldParentTopology"
                },
                statusCompatibilityDiagnostic: null,
                fastTrack);
            var generation = new SupportGenerationSnapshot(
                new[] { "game.build", "temperatureLimit.settings" },
                new[] { "game.gameVersion" },
                Array.Empty<string>(),
                issueSummaryWasShortened: false);
            var privacy = new SupportPrivacySnapshot(
                new[] { "game and mod versions", "mod settings" },
                new[] { "absolute paths", "save data" },
                Array.Empty<string>(),
                kind == SupportReportKind.Standard
                    ? Array.Empty<string>()
                    : new[] { "Player.log text" });

            return new SupportReportDocument(
                reportId,
                generatedAtUtc ?? GeneratedAtUtc,
                kind,
                game,
                temperatureLimit,
                runtime,
                activeMods ??
                    new[] { CreateActiveMod(0, "Temperature Limit") },
                omittedActiveModCount: 0,
                diagnostics ??
                    new[] { CreateDiagnostic("DTL-INITIALIZED") },
                omittedDistinctDiagnosticCount: 0,
                playerLog,
                generation,
                privacy);
        }

        internal static SupportActiveModSnapshot CreateActiveMod(
            int loadOrderIndex,
            string title) =>
            new(
                loadOrderIndex,
                title,
                SupportReportFact.Available("mod." + loadOrderIndex),
                SupportReportFact.Unavailable(
                    "No declared version was published."),
                new[] { "ExampleAssembly 1.0.0.0" },
                "local");

        internal static SupportDiagnosticSnapshot CreateDiagnostic(
            string code) =>
            new(
                code,
                "information",
                GeneratedAtUtc,
                GeneratedAtUtc,
                repeatCount: 1,
                "Sample diagnostic.",
                exceptionType: null,
                exceptionMessage: null);
    }
}
