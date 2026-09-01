namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;

/// <summary>
/// Constrains the cold composition root that prepares declared integrations and
/// consumes one provider-neutral capability selection.
/// Behavioral selection and incompatibility cases live in
/// <see cref="RuntimePatchInstallation.DeliveryTemperatureRuntimePatchPlanTests"/>;
/// these tests deliberately avoid repeating that matrix and instead prove the
/// runtime glue cannot bypass its result.
/// </summary>
[TestClass]
public sealed class FastTrackCoherentActivationContractTests
{
    [TestMethod]
    public void TopologyDependentInstallation_WhenInspected_VerifiesCompletePlanBeforeFirstSelectedPatchApplication()
    {
        string installerSource = ReadProductionSource(
            "RuntimePatchInstallation",
            "DeliveryTemperatureRuntimePatchInstaller.cs");
        string installationMethod = ExtractSourceRegion(
            installerSource,
            "internal static void InstallLoadedModTopologyDependentPatches(",
            "internal static bool TryStartAuthorizedGameSession(");

        int loadedModContextIndex = RequireIndex(
            installationMethod,
            "CreateLoadedModInspectionContext(");
        int declaredPreparationIndex = RequireIndex(
            installationMethod,
            "DeclaredExternalModIntegrationPreparation.Prepare(");
        int capabilitySelectionIndex = RequireIndex(
            installationMethod,
            "RuntimePatchCapabilitySelector.Select(");
        int planCreationIndex = RequireIndex(
            installationMethod,
            "DeliveryTemperatureRuntimePatchPlan.Create(");
        int authorityVerificationIndex = RequireIndex(
            installationMethod,
            "patchPlan.VerifySelectedAuthority(startupActivePrefixes)");
        int preparationIndex = RequireIndex(
            installationMethod,
            "PrepareSelectedRuntimePatches(");
        int applicationIndex = RequireIndex(
            installationMethod,
            "ApplyPreparedPatchesWithExactRollback(");

        Assert.IsTrue(
            loadedModContextIndex < declaredPreparationIndex &&
            declaredPreparationIndex < capabilitySelectionIndex &&
            capabilitySelectionIndex < planCreationIndex &&
            planCreationIndex < authorityVerificationIndex &&
            authorityVerificationIndex < preparationIndex &&
            preparationIndex < applicationIndex,
            "Loaded-mod copying, declared preparation, immutable capability " +
            "selection, authority proof, binding retrieval, and mutation must " +
            "remain in that exact order.");
        Assert.AreEqual(
            1,
            CountOrdinalOccurrences(
                installationMethod,
                "ApplyPreparedPatchesWithExactRollback("),
            "The topology-dependent transaction must have one mutation gate.");
        StringAssert.Contains(
            installationMethod,
            "runtimeInstallerState = RuntimePatchInstallerState.Failed");
        StringAssert.Contains(
            installationMethod,
            "throw;");
    }

    [TestMethod]
    public void SelectedPatchPreparation_WhenInspected_ReturnsOnlyPlanOwnedVerifiedBindings()
    {
        string installerSource = ReadProductionSource(
            "RuntimePatchInstallation",
            "DeliveryTemperatureRuntimePatchInstaller.cs");
        string preparationMethod = ExtractSourceRegion(
            installerSource,
            "private static HarmonyPatchContractBindingVerifier.VerifiedBindings\n            PrepareSelectedRuntimePatches(",
            "private static void PrepareGameSessionLifecyclePatches(");

        StringAssert.Contains(
            preparationMethod,
            "return patchPlan.OrderedPatchBindings;");
        Assert.IsFalse(
            preparationMethod.Contains(
                "switch (",
                StringComparison.Ordinal),
            "The installer must not dispatch on selected provider or group " +
            "identity after the plan is complete.");
        Assert.IsFalse(
            preparationMethod.Contains(
                "FastTrack",
                StringComparison.Ordinal),
            "Selected binding retrieval must be provider-neutral.");
        Assert.IsFalse(
            preparationMethod.Contains(
                "DeliveryTemperatureRuntimePatchGroup",
                StringComparison.Ordinal),
            "The deleted provider-shaped patch-group enum must not survive in " +
            "selected binding retrieval.");
    }

    [TestMethod]
    public void PatchApplication_WhenInspected_RequiresVerifierIssuedSnapshot()
    {
        string installerSource = ReadProductionSource(
            "RuntimePatchInstallation",
            "DeliveryTemperatureRuntimePatchInstaller.cs");

        StringAssert.Contains(
            installerSource,
            "private static void ApplyPreparedPatchesWithExactRollback(\n            Harmony harmony,\n            HarmonyPatchContractBindingVerifier.VerifiedBindings\n                preparedPatches)");
        Assert.AreEqual(
            1,
            CountOrdinalOccurrences(
                installerSource,
                "return HarmonyPatchContractBindingVerifier.VerifyAll("),
            "Only the topology-independent transaction remains verified in the " +
            "installer; the selected plan already owns its verified snapshot.");
    }

    [TestMethod]
    public void GameLoadAuthorityPath_WhenInspected_RechecksOwnersBeforePublishingOneSessionPerLoadIdentity()
    {
        string installerSource = ReadProductionSource(
            "RuntimePatchInstallation",
            "DeliveryTemperatureRuntimePatchInstaller.cs");
        string gameLoadMethod = ExtractSourceRegion(
            installerSource,
            "internal static bool TryStartAuthorizedGameSession(Game game)",
            "internal static IReadOnlyList<ActiveHarmonyPrefixDescriptor>");

        int repeatedIdentityIndex = RequireIndex(
            gameLoadMethod,
            "ReferenceEquals(evaluatedGame, game)");
        int descriptorCollectionIndex = RequireIndex(
            gameLoadMethod,
            "CollectActiveHarmonyPrefixDescriptors()");
        int authorityVerificationIndex = RequireIndex(
            gameLoadMethod,
            "patchPlan.VerifySelectedAuthority(activePrefixes)");
        int namedCatchIndex = RequireIndex(
            gameLoadMethod,
            "catch (HarmonyPatchContractViolationException exception)");
        int sessionPublicationIndex = RequireIndex(
            gameLoadMethod,
            "DeliveryTemperatureGameSessionHost.EnsureGameSession(");

        Assert.IsTrue(
            repeatedIdentityIndex < descriptorCollectionIndex &&
            descriptorCollectionIndex < authorityVerificationIndex &&
            authorityVerificationIndex < namedCatchIndex &&
            namedCatchIndex < sessionPublicationIndex,
            "A repeated load must return before reflection; a new load must " +
            "collect, verify, handle the named rejection, and only then publish.");
        Assert.AreEqual(
            1,
            CountOrdinalOccurrences(
                gameLoadMethod,
                "DeliveryTemperatureGameSessionHost.EnsureGameSession("),
            "There must be one game-session publication call site.");
        Assert.AreEqual(
            1,
            CountOrdinalOccurrences(gameLoadMethod, "catch ("),
            "The game-load boundary may catch only the named authority violation.");
        Assert.IsFalse(
            gameLoadMethod.Contains(
                "FastTrackCompatibilityInspector",
                StringComparison.Ordinal),
            "A game load rechecks owners but never reruns compatibility selection.");
        StringAssert.Contains(
            gameLoadMethod,
            "CacheGameLoadAuthorityOutcome(game, wasAuthorized: false)");
        StringAssert.Contains(
            gameLoadMethod,
            "CacheGameLoadAuthorityOutcome(game, wasAuthorized: true)");
    }

    [TestMethod]
    public void RuntimeCompatibilitySources_WhenInspected_ContainNoForeignUnpatchGuardForcingOrCompatibilityFacade()
    {
        string runtimeInstallationSource = ReadProductionDirectory(
            "RuntimePatchInstallation");
        string fastTrackCompatibilitySource = ReadProductionDirectory(
            "FastTrackCompatibility");
        string combinedSource = runtimeInstallationSource +
            Environment.NewLine +
            fastTrackCompatibilitySource;

        Assert.AreEqual(
            1,
            CountOrdinalOccurrences(combinedSource, "harmony.Unpatch("),
            "Only the exact-method rollback loop may unpatch anything.");
        StringAssert.Contains(
            runtimeInstallationSource,
            "harmony.Unpatch(\n                    installedPatch.TargetMethod,\n                    installedPatch.PatchMethod)");
        foreach (string forbiddenImplementation in new[]
                 {
                     "UnpatchAll(",
                     "HarmonyPatchType.All",
                     ".SetValue(",
                     "PatchAll(",
                     "TemperatureIndexData",
                     "getTemperatureIndexData",
                     "CompatibilityFacade",
                     "CompatibilityShim",
                     "ForceFastTrack"
                 })
        {
            Assert.IsFalse(
                combinedSource.Contains(
                    forbiddenImplementation,
                    StringComparison.Ordinal),
                "Forbidden compatibility implementation was found: " +
                forbiddenImplementation);
        }
    }

    private static string ReadProductionDirectory(string semanticDirectory)
    {
        string directory = Path.Combine(
            RequireRepositoryRoot(),
            "mods",
            "delivery-temperature-limit-supercooled",
            "Source",
            semanticDirectory);
        Assert.IsTrue(
            Directory.Exists(directory),
            "Missing production semantic directory: " + directory);
        return string.Join(
            Environment.NewLine,
            Directory.GetFiles(
                    directory,
                    "*.cs",
                    SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => NormalizeLineEndings(File.ReadAllText(path))));
    }

    private static string ReadProductionSource(
        string semanticDirectory,
        string sourceFileName)
    {
        string sourcePath = Path.Combine(
            RequireRepositoryRoot(),
            "mods",
            "delivery-temperature-limit-supercooled",
            "Source",
            semanticDirectory,
            sourceFileName);
        Assert.IsTrue(
            File.Exists(sourcePath),
            "Missing production source: " + sourcePath);
        return NormalizeLineEndings(File.ReadAllText(sourcePath));
    }

    private static string RequireRepositoryRoot()
    {
        string? repositoryRoot = Environment.GetEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            throw new AssertFailedException(
                "The ONI mod pipeline must provide " +
                "ONI_MOD_PIPELINE_REPOSITORY_ROOT.");
        }

        return repositoryRoot;
    }

    private static string ExtractSourceRegion(
        string source,
        string startMarker,
        string endMarker)
    {
        int startIndex = RequireIndex(source, startMarker);
        int endIndex = source.IndexOf(
            endMarker,
            startIndex + startMarker.Length,
            StringComparison.Ordinal);
        Assert.IsGreaterThan(
            startIndex,
            endIndex,
            "The exact source region must end after it starts.");
        return source.Substring(startIndex, endIndex - startIndex);
    }

    private static int RequireIndex(string source, string marker)
    {
        int index = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(
            0,
            index,
            "Missing exact source marker: " + marker);
        return index;
    }

    private static int CountOrdinalOccurrences(string source, string value)
    {
        int occurrenceCount = 0;
        int searchIndex = 0;
        while ((searchIndex = source.IndexOf(
                   value,
                   searchIndex,
                   StringComparison.Ordinal)) >= 0)
        {
            occurrenceCount++;
            searchIndex += value.Length;
        }

        return occurrenceCount;
    }

    private static string NormalizeLineEndings(string source) =>
        source.Replace("\r\n", "\n", StringComparison.Ordinal);
}
