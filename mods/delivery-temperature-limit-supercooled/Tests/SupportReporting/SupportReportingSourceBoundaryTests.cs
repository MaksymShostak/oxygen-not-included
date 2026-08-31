namespace DeliveryTemperatureLimit.Tests.SupportReporting;

[TestClass]
public sealed class SupportReportingSourceBoundaryTests
{
    private static readonly string[] RequiredIntegrationFiles =
    [
        "KleiSupportReportSnapshotReader.cs",
        "SupportReportJsonFileWriter.cs",
        "SupportReportPlayerPresenter.cs",
        "DeliveryTemperatureSupportReporter.cs"
    ];

    [TestMethod]
    public void SupportReportingSource_WhenCapabilitiesAreInspected_HasNoUploadTelemetryOrBroadMachineInspection()
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
            Assert.DoesNotContain(
                forbiddenToken,
                source,
                StringComparison.OrdinalIgnoreCase,
                $"Support reporting must not acquire forbidden capability '{forbiddenToken}'.");
        }
    }

    [TestMethod]
    public void SnapshotReader_WhenModFactsAreInspected_UsesSanitizedSupportedMembersWithoutPaths()
    {
        string source = ReadIntegrationFile(
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
            Assert.Contains(requiredMemberToken, source);
        }

        Assert.DoesNotContain(".path", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".file_source", source, StringComparison.Ordinal);
        Assert.DoesNotContain("label.path", source, StringComparison.Ordinal);
    }

    [TestMethod]
    public void SnapshotReader_WhenPlayerLogIsInspected_GatesConsolePathBehindExtendedKind()
    {
        string source = ReadIntegrationFile(
            "KleiSupportReportSnapshotReader.cs");

        Assert.AreEqual(
            1,
            CountOccurrences(source, "Application.consoleLogPath"));
        Assert.Contains(
            "SupportReportKind.ExtendedPlayerLog",
            source);
        Assert.Contains("CreatePlayerLogSnapshot", source);
    }

    [TestMethod]
    public void FileWriter_WhenDestinationAndSerializationAreInspected_UsesFixedLocalUtf8JsonContract()
    {
        string source = ReadIntegrationFile(
            "SupportReportJsonFileWriter.cs");

        Assert.Contains("Application.persistentDataPath", source);
        Assert.Contains("\"DeliveryTemperatureLimit\"", source);
        Assert.Contains("\"support-reports\"", source);
        Assert.Contains("JsonConvert.SerializeObject", source);
        Assert.Contains("Formatting.Indented", source);
        Assert.Contains("new UTF8Encoding(false)", source);
        Assert.Contains("MaximumReportBytes", source);
        Assert.Contains("File.Move", source);
        Assert.DoesNotContain("overwrite: true", source);
    }

    [TestMethod]
    public void FileWriter_WhenTemporaryFileIsPromoted_FlushesIntermediateBuffersToDiskFirst()
    {
        string source = ReadIntegrationFile(
            "SupportReportJsonFileWriter.cs");
        string writeMethod = ExtractMethod(source, "Write(");

        int flushToDisk = RequireIndex(
            writeMethod,
            "stream.Flush(flushToDisk: true);",
            0);
        int promote = RequireIndex(
            writeMethod,
            "File.Move(temporaryPath, finalPath);",
            flushToDisk);

        Assert.IsTrue(flushToDisk < promote);
        Assert.DoesNotContain("stream.Flush();", writeMethod);
    }

    [TestMethod]
    public void FileWriter_WhenSerializedReportIsTooLarge_UsesAdaptiveSizeLimiterBeforeWriting()
    {
        string source = ReadIntegrationFile(
            "SupportReportJsonFileWriter.cs");
        string writeMethod = ExtractMethod(source, "Write(");

        int adaptiveSizing = RequireIndex(
            writeMethod,
            "SupportJsonReportSizeLimiter.SerializeWithinLimit(",
            0);
        int temporaryFileWrite = RequireIndex(
            writeMethod,
            "new FileStream(",
            adaptiveSizing);

        Assert.IsTrue(adaptiveSizing < temporaryFileWrite);
    }

    [TestMethod]
    public void Presenter_WhenPlayerFlowIsInspected_UsesClipboardFixedBrowserAndVisibleDialog()
    {
        string source = ReadIntegrationFile(
            "SupportReportPlayerPresenter.cs");
        string successFlow = ExtractMethod(source, "PresentSuccess(");

        Assert.Contains("GUIUtility.systemCopyBuffer", source);
        Assert.Contains("Application.OpenURL", source);
        Assert.Contains("KMod.Manager.Dialog", source);
        Assert.Contains("SupportReportLimits.BugIssueOrigin", source);
        Assert.AreEqual(
            4,
            CountOccurrences(successFlow, "TryPresentationStep("),
            "Clipboard, folder, browser, and success dialog presentation " +
            "must each have an independent failure boundary.");
        Assert.Contains("DTL-SUPPORT-DIALOG-FAILED", successFlow);
    }

    [TestMethod]
    public void BugIssueForm_WhenSupportGuidanceIsOpened_TargetsRepositorySupportFile()
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
        Assert.IsGreaterThanOrEqualTo(0, destinationStart);
        destinationStart += linkMarker.Length;
        int destinationEnd = template.IndexOf(
            ')',
            destinationStart);
        Assert.IsGreaterThan(destinationStart, destinationEnd);

        string destination = template.Substring(
            destinationStart,
            destinationEnd - destinationStart);
        var renderedIssueForm = new Uri(
            "https://github.com/MaksymShostak/oxygen-not-included/issues/new" +
            "?template=temperature-limit-bug.yml");
        var resolvedDestination = new Uri(renderedIssueForm, destination);

        Assert.AreEqual(
            "https://github.com/MaksymShostak/oxygen-not-included/blob/main/SUPPORT.md",
            resolvedDestination.AbsoluteUri);
    }

    [TestMethod]
    public void Reporter_WhenActionBoundaryIsInspected_ContainsEveryFailureWithoutRethrowingToPLib()
    {
        string source = ReadIntegrationFile(
            "DeliveryTemperatureSupportReporter.cs");

        Assert.Contains("CreateStandardReport", source);
        Assert.Contains("CreateExtendedReport", source);
        Assert.Contains("catch (Exception exception)", source);
        Assert.Contains("PresentFailure", source);
        Assert.DoesNotContain("throw;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("throw exception", source, StringComparison.Ordinal);
    }

    [TestMethod]
    public void RuntimeInstaller_WhenSupportStateIsInspected_PublishesExistingPlanUnderItsLock()
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

        Assert.Contains("CaptureSupportReportSnapshot", source);
        Assert.Contains("lock (InstallationSynchronization)", source);
        Assert.Contains("installedPatchPlan", source);
        Assert.Contains("CreateSupportReportSnapshot", source);
        Assert.DoesNotContain(
            "FastTrackCompatibilityInspector",
            ExtractMethod(source, "CaptureSupportReportSnapshot"));
        Assert.DoesNotContain(
            "CollectActiveHarmonyPrefixDescriptors",
            ExtractMethod(source, "CaptureSupportReportSnapshot"));
    }

    [TestMethod]
    public void ModLifecycle_WhenSupportReportingIsInspected_InitializesAndPublishesBeforeRiskyPatchWork()
    {
        string source = ReadSourceFile("DeliveryTemperatureLimitMod.cs");

        int baseOnLoad = RequireIndex(source, "base.OnLoad(harmony);", 0);
        int initialize = RequireIndex(
            source,
            "DeliveryTemperatureSupportReporter.Initialize(mod, assembly);",
            baseOnLoad);
        int independentInstall = RequireIndex(
            source,
            "InstallLoadedModTopologyIndependentPatches(harmony)",
            initialize);
        int independentFailure = RequireIndex(
            source,
            "DTL-PATCH-TOPOLOGY-INDEPENDENT-FAILED",
            independentInstall);

        int baseAllModsLoaded = RequireIndex(
            source,
            "base.OnAllModsLoaded(harmony, loadedMods);",
            independentFailure);
        int publishLoadedMods = RequireIndex(
            source,
            "DeliveryTemperatureSupportReporter.PublishLoadedMods(loadedMods);",
            baseAllModsLoaded);
        int dependentInstall = RequireIndex(
            source,
            "InstallLoadedModTopologyDependentPatches(",
            publishLoadedMods);
        int dependentFailure = RequireIndex(
            source,
            "DTL-PATCH-TOPOLOGY-DEPENDENT-FAILED",
            dependentInstall);

        Assert.IsTrue(baseOnLoad < initialize && initialize < independentInstall);
        Assert.IsTrue(
            baseAllModsLoaded < publishLoadedMods &&
            publishLoadedMods < dependentInstall);
        Assert.IsTrue(independentInstall < independentFailure);
        Assert.IsTrue(dependentInstall < dependentFailure);
        Assert.AreEqual(2, CountOccurrences(source, "catch (Exception exception)"));
        Assert.AreEqual(2, CountOccurrences(source, "throw;"));
    }

    [TestMethod]
    public void OperationalDiagnostics_WhenSupportReportingIsInspected_UseStableCodesWithoutDuplicateUnityLogging()
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
            string source = ReadSourceFile(path);
            Assert.AreEqual(
                1,
                CountOccurrences(source, code),
                $"Stable diagnostic code {code} must occur exactly once.");
            Assert.Contains("DeliveryTemperatureSupportReporter.Record", source);
            Assert.Contains(severity, source);
            Assert.DoesNotContain(
                "Debug.Log",
                source,
                StringComparison.Ordinal,
                $"{string.Join('/', path)} must let the reporter mirror diagnostics once.");
        }
    }

    private static string ReadCombinedIntegrationSource() => string.Join(
        "\n",
        RequiredIntegrationFiles.Select(ReadIntegrationFile));

    private static string ReadIntegrationFile(string fileName)
    {
        string path = Path.Combine(
            ResolveRepositoryRoot(),
            "mods",
            "delivery-temperature-limit-supercooled",
            "Source",
            "SupportReporting",
            "KleiIntegration",
            fileName);
        Assert.IsTrue(File.Exists(path), $"Required integration source is missing: {path}");
        return File.ReadAllText(path);
    }

    private static string ReadSourceFile(params string[] relativePathSegments)
    {
        string path = relativePathSegments.Aggregate(
            Path.Combine(
                ResolveRepositoryRoot(),
                "mods",
                "delivery-temperature-limit-supercooled",
                "Source"),
            Path.Combine);
        Assert.IsTrue(File.Exists(path), $"Required source file is missing: {path}");
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

    private static string ExtractMethod(string source, string methodName)
    {
        int methodStart = source.IndexOf(
            methodName,
            StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, methodStart);
        int openingBrace = source.IndexOf('{', methodStart);
        Assert.IsGreaterThanOrEqualTo(0, openingBrace);
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

    private static int RequireIndex(
        string source,
        string value,
        int startIndex)
    {
        int index = source.IndexOf(value, startIndex, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(
            0,
            index,
            "Missing exact source contract marker: " + value);
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
