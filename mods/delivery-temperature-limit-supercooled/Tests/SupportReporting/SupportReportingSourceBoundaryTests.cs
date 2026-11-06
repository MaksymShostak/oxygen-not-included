namespace DeliveryTemperatureLimit.Tests.SupportReporting;
public sealed class SupportReportingSourceBoundaryTests
{
    private static readonly string[] RequiredIntegrationFiles =
    [
        "KleiSupportReportSnapshotReader.cs",
        "SupportReportJsonFileWriter.cs",
        "SupportReportPlayerPresenter.cs",
        "DeliveryTemperatureSupportReporter.cs"
    ];

    [Test]
    public async Task SupportReportingSource_WhenCapabilitiesAreInspected_HasNoUploadTelemetryOrBroadMachineInspection()
    {
        string source = ReadCombinedIntegrationSource();
        string[] forbiddenTokens =
        [
            "System.Net.Http",
            "HttpClient",
            "WebClient",
            "WebRequest",
            "System.Net.Sockets",
            "Socket(",
            "GitHubToken",
            "AuthorizationHeader",
            "TelemetryClient",
            "Environment.GetEnvironmentVariables",
            "Directory.EnumerateFiles",
            "Directory.GetFiles",
            "SaveLoader",
            "SaveGame"
        ];

        foreach (string forbiddenToken in forbiddenTokens)
        {
            await Assert.That(source).DoesNotContain(forbiddenToken, StringComparison.OrdinalIgnoreCase).Because($"Support reporting must not acquire forbidden capability '{forbiddenToken}'.");
        }
    }

    [Test]
    public async Task SnapshotReader_WhenModFactsAreInspected_UsesSanitizedSupportedMembersWithoutPaths()
    {
        string source = await ReadIntegrationFile(
            "KleiSupportReportSnapshotReader.cs");

        string[] requiredMemberTokens =
        [
            ".title",
            ".staticID",
            ".packagedModInfo",
            ".IsActive()",
            ".loaded_mod_data.dlls"
        ];
        foreach (string requiredMemberToken in requiredMemberTokens)
        {
            await Assert.That(source).Contains(requiredMemberToken);
        }

        await Assert.That(source).DoesNotContain(".path", StringComparison.Ordinal);
        await Assert.That(source).DoesNotContain(".file_source", StringComparison.Ordinal);
        await Assert.That(source).DoesNotContain("label.path", StringComparison.Ordinal);
    }

    [Test]
    public async Task SnapshotReader_WhenPlayerLogIsInspected_GatesConsolePathBehindExtendedKind()
    {
        string source = await ReadIntegrationFile(
            "KleiSupportReportSnapshotReader.cs");

        await Assert.That(CountOccurrences(source, "Application.consoleLogPath")).IsEqualTo(1);
        await Assert.That(source).Contains("SupportReportKind.ExtendedPlayerLog");
        await Assert.That(source).Contains("CreatePlayerLogSnapshot");
    }

    [Test]
    public async Task FileWriter_WhenDestinationAndSerializationAreInspected_UsesFixedLocalUtf8JsonContract()
    {
        string source = await ReadIntegrationFile(
            "SupportReportJsonFileWriter.cs");

        await Assert.That(source).Contains("Application.persistentDataPath");
        await Assert.That(source).Contains("\"DeliveryTemperatureLimit\"");
        await Assert.That(source).Contains("\"support-reports\"");
        await Assert.That(source).Contains("JsonConvert.SerializeObject");
        await Assert.That(source).Contains("Formatting.Indented");
        await Assert.That(source).Contains("new UTF8Encoding(false)");
        await Assert.That(source).Contains("MaximumReportBytes");
        await Assert.That(source).Contains("File.Move");
        await Assert.That(source).DoesNotContain("overwrite: true");
    }

    [Test]
    public async Task FileWriter_WhenTemporaryFileIsPromoted_FlushesIntermediateBuffersToDiskFirst()
    {
        string source = await ReadIntegrationFile(
            "SupportReportJsonFileWriter.cs");
        string writeMethod = await ExtractMethod(source, "Write(");

        int flushToDisk = await RequireIndex(
            writeMethod,
            "stream.Flush(flushToDisk: true);",
            0);
        int promote = await RequireIndex(
            writeMethod,
            "File.Move(temporaryPath, finalPath);",
            flushToDisk);

        await Assert.That(flushToDisk < promote).IsTrue();
        await Assert.That(writeMethod).DoesNotContain("stream.Flush();");
    }

    [Test]
    public async Task FileWriter_WhenSerializedReportIsTooLarge_UsesAdaptiveSizeLimiterBeforeWriting()
    {
        string source = await ReadIntegrationFile(
            "SupportReportJsonFileWriter.cs");
        string writeMethod = await ExtractMethod(source, "Write(");

        int adaptiveSizing = await RequireIndex(
            writeMethod,
            "SupportJsonReportSizeLimiter.SerializeWithinLimit(",
            0);
        int temporaryFileWrite = await RequireIndex(
            writeMethod,
            "new FileStream(",
            adaptiveSizing);

        await Assert.That(adaptiveSizing < temporaryFileWrite).IsTrue();
    }

    [Test]
    public async Task Presenter_WhenPlayerFlowIsInspected_UsesClipboardFixedBrowserAndExplicitActions()
    {
        string source = await ReadIntegrationFile(
            "SupportReportPlayerPresenter.cs");

        await Assert.That(source).Contains("GUIUtility.systemCopyBuffer");
        await Assert.That(source).Contains("Application.OpenURL");
        // Only SupportReportSession builds the fixed-origin, bounded, prefilled URL.
        await Assert.That(source).Contains("OpenIssueForm(string preparedUrl)");
        await Assert.That(source).Contains("OpenLastReportFolder");
        await Assert.That(source).Contains("CopyLastReportSummary");
        await Assert.That(source).Contains("OpenIssueForm");
        await Assert.That(source).Contains("Run(");
    }

    [Test]
    public async Task BugIssueForm_WhenSupportGuidanceIsOpened_TargetsRepositorySupportFile()
    {
        string templatePath = Path.Combine(
            ResolveRepositoryRoot(),
            ".github",
            "ISSUE_TEMPLATE",
            "temperature-limit-bug.yml");
        string template = File.ReadAllText(templatePath);
        const string linkMarker = "[SUPPORT.md](";
        int destinationStart = template.IndexOf(
            linkMarker,
            StringComparison.Ordinal);
        await Assert.That(destinationStart).IsGreaterThanOrEqualTo(0);
        destinationStart += linkMarker.Length;
        int destinationEnd = template.IndexOf(
            ')',
            destinationStart);
        await Assert.That(destinationEnd).IsGreaterThan(destinationStart);

        string destination = template.Substring(
            destinationStart,
            destinationEnd - destinationStart);
        var renderedIssueForm = new Uri(
            "https://github.com/MaksymShostak/oxygen-not-included/issues/new" +
            "?template=temperature-limit-bug.yml");
        var resolvedDestination = new Uri(renderedIssueForm, destination);

        await Assert.That(resolvedDestination.AbsoluteUri).IsEqualTo("https://github.com/MaksymShostak/oxygen-not-included/blob/main/SUPPORT.md");
    }

    [Test]
    public async Task Reporter_WhenActionBoundaryIsInspected_ContainsEveryFailureWithoutRethrowingToPLib()
    {
        string source = await ReadIntegrationFile(
            "DeliveryTemperatureSupportReporter.cs");

        await Assert.That(source).Contains("CreateStandardReport");
        await Assert.That(source).Contains("CreateExtendedReport");
        await Assert.That(source).Contains("catch (Exception exception)");
        await Assert.That(source).Contains("PresentFailure");
        await Assert.That(source).DoesNotContain("throw;", StringComparison.Ordinal);
        await Assert.That(source).DoesNotContain("throw exception", StringComparison.Ordinal);
    }

    [Test]
    public async Task RuntimeInstaller_WhenSupportStateIsInspected_PublishesExistingPlanUnderItsLock()
    {
        string repositoryRoot = ResolveRepositoryRoot();
        string installerPath = Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled",
            "Source",
            "RuntimePatchInstallation",
            "DeliveryTemperatureRuntimePatchInstaller.cs");
        string source = File.ReadAllText(installerPath);

        await Assert.That(source).Contains("CaptureSupportReportSnapshot");
        await Assert.That(source).Contains("lock (InstallationSynchronization)");
        await Assert.That(source).Contains("installedPatchPlan");
        await Assert.That(source).Contains("CreateSupportReportSnapshot");
        await Assert.That((await ExtractMethod(source, "CaptureSupportReportSnapshot"))).DoesNotContain("FastTrackCompatibilityInspector");
        await Assert.That((await ExtractMethod(source, "CaptureSupportReportSnapshot"))).DoesNotContain("CollectActiveHarmonyPrefixDescriptors");
    }

    [Test]
    public async Task ModLifecycle_WhenSupportReportingIsInspected_InitializesAndPublishesBeforeRiskyPatchWork()
    {
        string source = await ReadSourceFile("DeliveryTemperatureLimitMod.cs");

        int baseOnLoad = await RequireIndex(source, "base.OnLoad(harmony);", 0);
        int initialize = await RequireIndex(
            source,
            "DeliveryTemperatureSupportReporter.Initialize(mod, assembly);",
            baseOnLoad);
        int independentInstall = await RequireIndex(
            source,
            "InstallLoadedModTopologyIndependentPatches(harmony)",
            initialize);
        int independentFailure = await RequireIndex(
            source,
            "DTL-PATCH-TOPOLOGY-INDEPENDENT-FAILED",
            independentInstall);

        int baseAllModsLoaded = await RequireIndex(
            source,
            "base.OnAllModsLoaded(harmony, loadedMods);",
            independentFailure);
        int publishLoadedMods = await RequireIndex(
            source,
            "DeliveryTemperatureSupportReporter.PublishLoadedMods(loadedMods);",
            baseAllModsLoaded);
        int dependentInstall = await RequireIndex(
            source,
            "InstallLoadedModTopologyDependentPatches(",
            publishLoadedMods);
        int dependentFailure = await RequireIndex(
            source,
            "DTL-PATCH-TOPOLOGY-DEPENDENT-FAILED",
            dependentInstall);

        await Assert.That(baseOnLoad < initialize && initialize < independentInstall).IsTrue();
        await Assert.That(baseAllModsLoaded < publishLoadedMods &&
            publishLoadedMods < dependentInstall).IsTrue();
        await Assert.That(independentInstall < independentFailure).IsTrue();
        await Assert.That(dependentInstall < dependentFailure).IsTrue();
        await Assert.That(CountOccurrences(source, "catch (Exception exception)")).IsEqualTo(2);
        await Assert.That(CountOccurrences(source, "throw;")).IsEqualTo(0).Because("Mod startup failures must be contained rather than rethrown into ONI.");
        await Assert.That(source).Contains("RuntimeFailureReporting.DisableGameplay(nameof(OnLoad), exception)");
        await Assert.That(source).Contains("RuntimeFailureReporting.DisableGameplay(nameof(OnAllModsLoaded), exception)");
    }

    [Test]
    public async Task OperationalDiagnostics_WhenSupportReportingIsInspected_UseStableCodesWithoutDuplicateUnityLogging()
    {
        (string[] Path, string Code, string Severity)[] diagnostics =
        [
            (
                ["TemperatureLimitedDeliveryTargets", "TemperatureLimitedDeliveryTargetPrefabConfigurator.cs"],
                "DTL-PREFAB-CONFIGURATION-SKIPPED",
                "SupportDiagnosticSeverity.Error"),
            (
                ["TemperatureLimitedDeliveryTargets", "TemperatureLimitedDeliveryTargetPrefabConfigurator.cs"],
                "DTL-PREFAB-CONFIGURATION-COMPLETE",
                "SupportDiagnosticSeverity.Information"),
            (
                ["TemperatureLimitUserInterface", "TemperatureLimitSideScreen.cs"],
                "DTL-SIDE-SCREEN-REGISTRATION-FAILED",
                "SupportDiagnosticSeverity.Error"),
            (
                ["TemperatureLimitedDeliveryTargets", "ConstructionMaterialTemperatureLimit.cs"],
                "DTL-BLUEPRINT-WIDGET-LATE-ATTACHED",
                "SupportDiagnosticSeverity.Warning"),
            (
                ["RuntimePatchInstallation", "DeliveryTemperatureRuntimePatchInstaller.cs"],
                "DTL-STATUS-COMPATIBILITY-DEGRADED",
                "SupportDiagnosticSeverity.Error"),
            (
                ["RuntimePatchInstallation", "DeliveryTemperatureRuntimePatchInstaller.cs"],
                "DTL-GAME-LOAD-AUTHORITY-REJECTED",
                "SupportDiagnosticSeverity.Error"),
            (
                ["FastTrackCompatibility", "InventoryUpdateAdapters", "FastTrackWorldInventoryTemperaturePatches.cs"],
                "DTL-FASTTRACK-INVENTORY-PUBLICATION-SKIPPED",
                "SupportDiagnosticSeverity.Warning")
        ];

        foreach ((string[] path, string code, string severity) in diagnostics)
        {
            string source = await ReadSourceFile(path);
            await Assert.That(CountOccurrences(source, code)).IsEqualTo(1).Because($"Stable diagnostic code {code} must occur exactly once.");
            await Assert.That(source).Contains("DeliveryTemperatureSupportReporter.Record");
            await Assert.That(source).Contains(severity);
            await Assert.That(source).DoesNotContain("Debug.Log", StringComparison.Ordinal).Because($"{string.Join('/', path)} must let the reporter mirror diagnostics once.");
        }
    }

    private static string ReadCombinedIntegrationSource() => string.Join(
        "\n",
        RequiredIntegrationFiles.Select(ReadIntegrationFile));

    private static async Task<string> ReadIntegrationFile(string fileName)
    {
        string path = Path.Combine(
            ResolveRepositoryRoot(),
            "mods",
            "delivery-temperature-limit-supercooled",
            "Source",
            "SupportReporting",
            "KleiIntegration",
            fileName);
        await Assert.That(File.Exists(path)).IsTrue().Because($"Required integration source is missing: {path}");
        return File.ReadAllText(path);
    }

    private static async Task<string> ReadSourceFile(params string[] relativePathSegments)
    {
        string path = relativePathSegments.Aggregate(
            Path.Combine(
                ResolveRepositoryRoot(),
                "mods",
                "delivery-temperature-limit-supercooled",
                "Source"),
            Path.Combine);
        await Assert.That(File.Exists(path)).IsTrue().Because($"Required source file is missing: {path}");
        return File.ReadAllText(path);
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int searchFrom = 0;
        while (searchFrom < source.Length)
        {
            int match = source.IndexOf(
                value,
                searchFrom,
                StringComparison.Ordinal);
            if (match < 0)
            {
                return count;
            }

            count++;
            searchFrom = match + value.Length;
        }

        return count;
    }

    private static async Task<string> ExtractMethod(string source, string methodName)
    {
        int methodStart = source.IndexOf(
            methodName,
            StringComparison.Ordinal);
        await Assert.That(methodStart).IsGreaterThanOrEqualTo(0);
        int openingBrace = source.IndexOf('{', methodStart);
        await Assert.That(openingBrace).IsGreaterThanOrEqualTo(0);
        int depth = 0;
        for (int index = openingBrace; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(
                        methodStart,
                        index - methodStart + 1);
                }
            }
        }

        Assert.Fail($"Method '{methodName}' has no balanced body.");
        return string.Empty;
    }

    private static async Task<int> RequireIndex(
        string source,
        string value,
        int startIndex)
    {
        int index = source.IndexOf(value, startIndex, StringComparison.Ordinal);
        await Assert.That(index).IsGreaterThanOrEqualTo(0).Because("Missing exact source contract marker: " + value);
        return index;
    }

    private static string ResolveRepositoryRoot()
    {
        string? pipelineRepositoryRoot = Environment.GetEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        if (!string.IsNullOrWhiteSpace(pipelineRepositoryRoot))
        {
            return pipelineRepositoryRoot;
        }

        DirectoryInfo? candidateDirectory = new(AppContext.BaseDirectory);
        while (candidateDirectory is not null)
        {
            string projectPath = Path.Combine(
                candidateDirectory.FullName,
                "mods",
                "delivery-temperature-limit-supercooled",
                "Tests",
                "DeliveryTemperatureLimit.Tests.csproj");
            if (File.Exists(projectPath))
            {
                return candidateDirectory.FullName;
            }

            candidateDirectory = candidateDirectory.Parent;
        }

        throw new InvalidOperationException(
            "The repository root was not supplied and could not be resolved " +
            $"from {AppContext.BaseDirectory}.");
    }
}
