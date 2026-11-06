namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;
public sealed class FastTrackCoherentActivationContractTests
{
    [Test]
    public async Task TopologyDependentInstallation_WhenInspected_VerifiesCompletePlanBeforeFirstSelectedPatchApplication()
    {
        string installerSource = await ReadProductionSource(
            "RuntimePatchInstallation",
            "DeliveryTemperatureRuntimePatchInstaller.cs");
        string installationMethod = await ExtractSourceRegion(
            installerSource,
            "internal static void InstallLoadedModTopologyDependentPatches(",
            "internal static bool TryStartAuthorizedGameSession(");

        int loadedModContextIndex = await RequireIndex(
            installationMethod,
            "CreateLoadedModInspectionContext(");
        int declaredPreparationIndex = await RequireIndex(
            installationMethod,
            "DeclaredExternalModIntegrationPreparation.Prepare(");
        int capabilitySelectionIndex = await RequireIndex(
            installationMethod,
            "RuntimePatchCapabilitySelector.Select(");
        int planCreationIndex = await RequireIndex(
            installationMethod,
            "DeliveryTemperatureRuntimePatchPlan.Create(");
        int authorityVerificationIndex = await RequireIndex(
            installationMethod,
            "patchPlan.VerifySelectedAuthority(startupActivePrefixes)");
        int preparationIndex = await RequireIndex(
            installationMethod,
            "PrepareSelectedRuntimePatches(");
        int applicationIndex = await RequireIndex(
            installationMethod,
            "ApplyPreparedPatchesWithExactRollback(");

        await Assert.That(loadedModContextIndex < declaredPreparationIndex &&
            declaredPreparationIndex < capabilitySelectionIndex &&
            capabilitySelectionIndex < planCreationIndex &&
            planCreationIndex < authorityVerificationIndex &&
            authorityVerificationIndex < preparationIndex &&
            preparationIndex < applicationIndex).IsTrue().Because("Loaded-mod copying, declared preparation, immutable capability " +
            "selection, authority proof, binding retrieval, and mutation must " +
            "remain in that exact order.");
        await Assert.That(CountOrdinalOccurrences(
                installationMethod,
                "ApplyPreparedPatchesWithExactRollback(")).IsEqualTo(1).Because("The topology-dependent transaction must have one mutation gate.");
        await Assert.That(installationMethod).Contains("runtimeInstallerState = RuntimePatchInstallerState.Failed");
        await Assert.That(installationMethod).Contains("throw;");
    }

    [Test]
    public async Task SelectedPatchPreparation_WhenInspected_ReturnsOnlyPlanOwnedVerifiedBindings()
    {
        string installerSource = await ReadProductionSource(
            "RuntimePatchInstallation",
            "DeliveryTemperatureRuntimePatchInstaller.cs");
        string preparationMethod = await ExtractSourceRegion(
            installerSource,
            "private static HarmonyPatchContractBindingVerifier.VerifiedBindings\n            PrepareSelectedRuntimePatches(",
            "private static void PrepareGameSessionLifecyclePatches(");

        await Assert.That(preparationMethod).Contains("return patchPlan.OrderedPatchBindings;");
        await Assert.That(preparationMethod.Contains(
                "switch (",
                StringComparison.Ordinal)).IsFalse().Because("The installer must not dispatch on selected provider or group " +
            "identity after the plan is complete.");
        await Assert.That(preparationMethod.Contains(
                "FastTrack",
                StringComparison.Ordinal)).IsFalse().Because("Selected binding retrieval must be provider-neutral.");
        await Assert.That(preparationMethod.Contains(
                "DeliveryTemperatureRuntimePatchGroup",
                StringComparison.Ordinal)).IsFalse().Because("The deleted provider-shaped patch-group enum must not survive in " +
            "selected binding retrieval.");
    }

    [Test]
    public async Task PatchApplication_WhenInspected_RequiresVerifierIssuedSnapshot()
    {
        string installerSource = await ReadProductionSource(
            "RuntimePatchInstallation",
            "DeliveryTemperatureRuntimePatchInstaller.cs");

        await Assert.That(installerSource).Contains("private static void ApplyPreparedPatchesWithExactRollback(\n            Harmony harmony,\n            HarmonyPatchContractBindingVerifier.VerifiedBindings\n                preparedPatches)");
        await Assert.That(CountOrdinalOccurrences(
                installerSource,
                "return HarmonyPatchContractBindingVerifier.VerifyAll(")).IsEqualTo(1).Because("Only the topology-independent transaction remains verified in the " +
            "installer; the selected plan already owns its verified snapshot.");
    }

    [Test]
    public async Task GameLoadAuthorityPath_WhenInspected_RechecksOwnersBeforePublishingOneSessionPerLoadIdentity()
    {
        string installerSource = await ReadProductionSource(
            "RuntimePatchInstallation",
            "DeliveryTemperatureRuntimePatchInstaller.cs");
        string gameLoadMethod = await ExtractSourceRegion(
            installerSource,
            "internal static bool TryStartAuthorizedGameSession(Game game)",
            "internal static IReadOnlyList<ActiveHarmonyPrefixDescriptor>");

        int repeatedIdentityIndex = await RequireIndex(
            gameLoadMethod,
            "ReferenceEquals(evaluatedGame, game)");
        int descriptorCollectionIndex = await RequireIndex(
            gameLoadMethod,
            "CollectActiveHarmonyPrefixDescriptors()");
        int authorityVerificationIndex = await RequireIndex(
            gameLoadMethod,
            "patchPlan.VerifySelectedAuthority(activePrefixes)");
        int namedCatchIndex = await RequireIndex(
            gameLoadMethod,
            "catch (HarmonyPatchContractViolationException exception)");
        int sessionPublicationIndex = await RequireIndex(
            gameLoadMethod,
            "DeliveryTemperatureGameSessionHost.EnsureGameSession(");

        await Assert.That(repeatedIdentityIndex < descriptorCollectionIndex &&
            descriptorCollectionIndex < authorityVerificationIndex &&
            authorityVerificationIndex < namedCatchIndex &&
            namedCatchIndex < sessionPublicationIndex).IsTrue().Because("A repeated load must return before reflection; a new load must " +
            "collect, verify, handle the named rejection, and only then publish.");
        await Assert.That(CountOrdinalOccurrences(
                gameLoadMethod,
                "DeliveryTemperatureGameSessionHost.EnsureGameSession(")).IsEqualTo(1).Because("There must be one game-session publication call site.");
        await Assert.That(CountOrdinalOccurrences(gameLoadMethod, "catch (")).IsEqualTo(1).Because("The game-load boundary may catch only the named authority violation.");
        await Assert.That(gameLoadMethod.Contains(
                "FastTrackCompatibilityInspector",
                StringComparison.Ordinal)).IsFalse().Because("A game load rechecks owners but never reruns compatibility selection.");
        await Assert.That(gameLoadMethod).Contains("CacheGameLoadAuthorityOutcome(game, wasAuthorized: false)");
        await Assert.That(gameLoadMethod).Contains("CacheGameLoadAuthorityOutcome(game, wasAuthorized: true)");
    }

    [Test]
    public async Task RuntimeCompatibilitySources_WhenInspected_ContainNoForeignUnpatchGuardForcingOrCompatibilityFacade()
    {
        string runtimeInstallationSource = await ReadProductionDirectory(
            "RuntimePatchInstallation");
        string fastTrackCompatibilitySource = await ReadProductionDirectory(
            "FastTrackCompatibility");
        string combinedSource = runtimeInstallationSource +
            Environment.NewLine +
            fastTrackCompatibilitySource;

        await Assert.That(CountOrdinalOccurrences(combinedSource, "harmony.Unpatch(")).IsEqualTo(1).Because("Only the exact-method rollback loop may unpatch anything.");
        await Assert.That(runtimeInstallationSource).Contains("harmony.Unpatch(\n                    installedPatch.TargetMethod,\n                    installedPatch.PatchMethod)");
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
            await Assert.That(combinedSource.Contains(
                    forbiddenImplementation,
                    StringComparison.Ordinal)).IsFalse().Because("Forbidden compatibility implementation was found: " +
                forbiddenImplementation);
        }
    }

    private static async Task<string> ReadProductionDirectory(string semanticDirectory)
    {
        string directory = Path.Combine(
            RequireRepositoryRoot(),
            "mods",
            "delivery-temperature-limit-supercooled",
            "Source",
            semanticDirectory);
        await Assert.That(Directory.Exists(directory)).IsTrue().Because("Missing production semantic directory: " + directory);
        return string.Join(
            Environment.NewLine,
            Directory.GetFiles(
                    directory,
                    "*.cs",
                    SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => NormalizeLineEndings(File.ReadAllText(path))));
    }

    private static async Task<string> ReadProductionSource(
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
        await Assert.That(File.Exists(sourcePath)).IsTrue().Because("Missing production source: " + sourcePath);
        return NormalizeLineEndings(File.ReadAllText(sourcePath));
    }

    private static string RequireRepositoryRoot()
    {
        string? repositoryRoot = Environment.GetEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            throw new TUnit.Assertions.Exceptions.AssertionException(
                "The ONI mod pipeline must provide " +
                "ONI_MOD_PIPELINE_REPOSITORY_ROOT.");
        }

        return repositoryRoot;
    }

    private static async Task<string> ExtractSourceRegion(
        string source,
        string startMarker,
        string endMarker)
    {
        int startIndex = await RequireIndex(source, startMarker);
        int endIndex = source.IndexOf(
            endMarker,
            startIndex + startMarker.Length,
            StringComparison.Ordinal);
        await Assert.That(endIndex).IsGreaterThan(startIndex).Because("The exact source region must end after it starts.");
        return source.Substring(startIndex, endIndex - startIndex);
    }

    private static async Task<int> RequireIndex(string source, string marker)
    {
        int index = source.IndexOf(marker, StringComparison.Ordinal);
        await Assert.That(index).IsGreaterThanOrEqualTo(0).Because("Missing exact source marker: " + marker);
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
