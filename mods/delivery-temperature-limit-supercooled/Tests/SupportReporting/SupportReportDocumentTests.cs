namespace DeliveryTemperatureLimit.Tests.SupportReporting;
public sealed class SupportReportDocumentTests
{
    [Test]
    public async Task Constructor_WhenStandardFactsAreSupplied_PreservesSchemaAndExplicitAvailability()
    {
        SupportReportDocument document = SupportReportDocumentFixture.Create(
            SupportReportKind.Standard,
            playerLog: null);

        await Assert.That(document.SchemaVersion).IsEqualTo(2);
        await Assert.That(document.ReportKind).IsEqualTo("standard");
        await Assert.That(document.Game.Build.State).IsEqualTo("available");
        await Assert.That(document.Game.Build.Value).IsEqualTo("744825");
        await Assert.That(document.Game.GameVersion.State).IsEqualTo("unavailable");
        await Assert.That(document.Game.ActiveDlcs.State).IsEqualTo("available");
        await Assert.That(document.PlayerLog).IsNull();
    }

    [Test]
    public async Task Constructor_WhenTemperatureUnitIsNonCelsius_PreservesUnitWithDisplayedThresholds()
    {
        SupportReportDocument document = SupportReportDocumentFixture.Create(
            SupportReportKind.Standard,
            playerLog: null,
            temperatureUnit: "Fahrenheit",
            maxConstructionTemperature: 113,
            minConstructionTemperature: -58);

        await Assert.That(document.TemperatureLimit.TemperatureUnit).IsEqualTo("Fahrenheit");
        await Assert.That(document.TemperatureLimit.MaxConstructionTemperature).IsEqualTo(113);
        await Assert.That(document.TemperatureLimit.MinConstructionTemperature).IsEqualTo(-58);
    }

    [Test]
    public async Task Constructor_WhenExtendedReportHasNoPlayerLog_RejectsIncompleteDocument()
    {
        await Assert.That(() =>
            SupportReportDocumentFixture.Create(
                SupportReportKind.ExtendedPlayerLog,
                playerLog: null)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Constructor_WhenStandardReportHasPlayerLog_RejectsUnexpectedContent()
    {
        SupportPlayerLogSnapshot playerLog =
            SupportPlayerLogSnapshot.Available(
                "unity-console-log-path",
                originalByteCount: 12,
                includedRawByteCount: 12,
                truncated: false,
                redactedPlaceholders: Array.Empty<string>(),
                content: "sample log");

        await Assert.That(() =>
            SupportReportDocumentFixture.Create(
                SupportReportKind.Standard,
                playerLog)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Constructor_WhenCallerMutatesInputLists_PreservesOriginalOrderAndValues()
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

        await Assert.That(document.Game.ActiveDlcs.Ids.ToArray()).IsEquivalentTo(new[] { "EXPANSION1_ID", "BASE_GAME" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(document.ActiveMods.Select(mod => mod.Title).ToArray()).IsEquivalentTo(new[] { "First mod", "Second mod" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(document.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray()).IsEquivalentTo(new[] { "DTL-FIRST", "DTL-SECOND" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Constructor_WhenIdentityOrTimestampIsInvalid_RejectsDocument()
    {
        await Assert.That(() =>
            SupportReportDocumentFixture.Create(
                SupportReportKind.Standard,
                playerLog: null,
                reportId: " ")).ThrowsExactly<ArgumentException>();
        await Assert.That(() =>
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
                    TimeSpan.FromHours(3)))).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task ReportKind_DefinesOnlyTheTwoSchemaVersionTwoModes()
    {
        await Assert.That(Enum.GetNames<SupportReportKind>()).IsEquivalentTo(new[] { "Standard", "ExtendedPlayerLog" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Enum.GetValues<SupportReportKind>()
                .Select(value => (int)value)
                .ToArray()).IsEquivalentTo(new[] { 0, 1 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task UnavailableFact_WhenReasonIsBlank_RejectsAmbiguousAbsence()
    {
        await Assert.That(() =>
            SupportReportFact.Unavailable(" ")).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task WithIssueSummaryWasShortened_WhenUrlWasBounded_ReturnsUpdatedImmutableDocument()
    {
        SupportReportDocument original = SupportReportDocumentFixture.Create(
            SupportReportKind.Standard,
            playerLog: null);

        SupportReportDocument updated =
            original.WithIssueSummaryWasShortened();

        await Assert.That(original.Generation.IssueSummaryWasShortened).IsFalse();
        await Assert.That(updated.Generation.IssueSummaryWasShortened).IsTrue();
        await Assert.That(updated.ReportId).IsEqualTo(original.ReportId);
        await Assert.That(updated.Game).IsSameReferenceAs(original.Game);
        await Assert.That(updated.TemperatureLimit).IsSameReferenceAs(original.TemperatureLimit);
        await Assert.That(updated.Runtime).IsSameReferenceAs(original.Runtime);
        await Assert.That(updated.ActiveMods.ToArray()).IsEquivalentTo(original.ActiveMods.ToArray(), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(updated.Diagnostics.ToArray()).IsEquivalentTo(original.Diagnostics.ToArray(), TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task SerializeWithinLimit_WhenExtendedReportIsTooLarge_KeepsNewestLogContentAndDisclosesShortening()
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

        await Assert.That(serialization.Json).IsEqualTo("ghij");
        await Assert.That(serialization.Utf8ByteCount).IsEqualTo(4);
        await Assert.That(serialization.Utf8ByteCount < 5).IsTrue();
        await Assert.That(original.PlayerLog!.Content).IsEqualTo("abcdefghij");
        await Assert.That(original.PlayerLog.Truncated).IsFalse();
        await Assert.That(serialization.Document.PlayerLog!.Content).IsEqualTo("ghij");
        await Assert.That(serialization.Document.PlayerLog.Truncated).IsTrue();
        await Assert.That(serialization.Document.Generation.Warnings.ToArray()).Contains("Player.log content was shortened further to keep the complete " +
                "report below 12 MiB.");
        await Assert.That(serialization.Document.Game).IsSameReferenceAs(original.Game);
        await Assert.That(serialization.Document.TemperatureLimit).IsSameReferenceAs(original.TemperatureLimit);
        await Assert.That(serialization.Document.Runtime).IsSameReferenceAs(original.Runtime);
    }

    [Test]
    public async Task SerializeWithinLimit_WhenDocumentAlreadyFits_ReturnsItUnchanged()
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

        await Assert.That(serialization.Document).IsSameReferenceAs(original);
        await Assert.That(serialization.Json).IsEqualTo("abc");
        await Assert.That(serialization.Utf8ByteCount).IsEqualTo(3);
        await Assert.That(serialization.Document.PlayerLog!.Truncated).IsFalse();
        await Assert.That(serialization.Document.Generation.Warnings.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SerializeWithinLimit_WhenNonLogDocumentIsAtLimit_RejectsReport()
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

        await Assert.That(() =>
            SupportJsonReportSizeLimiter.SerializeWithinLimit(
                document,
                maximumByteCount: 5,
                candidate =>
                    "fixed" + (candidate.PlayerLog!.Content ?? string.Empty))).ThrowsExactly<InvalidOperationException>();
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
                new[]
                {
                    "GameSessionLifecycle",
                    "WorldParentTopology"
                },
                statusCompatibilityDiagnostic: null,
                new[] { fastTrack });
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
