namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;
public sealed class FastTrackPickupTemperaturePatchContractTests
{
    [Test]
    public async Task AdapterSource_WhenInspected_BindsReadyFeatureAndChangesOnlyConstructorHashArgument()
    {
        string sourcePath = ResolveProductionSourcePath(
            "PickupGroupingAdapters",
            "FastTrackPickupTemperaturePatches.cs");
        await Assert.That(File.Exists(sourcePath)).IsTrue().Because($"Missing inactive FastTrack pickup adapter {sourcePath}.");
        string source = File.ReadAllText(sourcePath);

        await Assert.That(source).Contains("BindVerifiedPickupGroupingFeature");
        await Assert.That(source).Contains("FastTrackFeature.PickupGrouping");
        await Assert.That(source).Contains("FastTrackFeatureCompatibilityState.Ready");
        await Assert.That(source).Contains("ResolveFetchManagerBeforeUpdatePickupsTarget");
        await Assert.That(source).Contains("ResolvePickupTagDictionaryAddItemTarget");
        await Assert.That(source).Contains("BeforeUpdatePickupsPrefix");
        await Assert.That(source).Contains("BeforeUpdatePickupsPostfix");
        await Assert.That(source).Contains("BeforeUpdatePickupsFinalizer");
        await Assert.That(source).Contains("PickupTagDictionaryAddItemTranspiler");
        await Assert.That(source).Contains("PickupTemperatureGroupingSession");
        await Assert.That(source).Contains("FastTrackPickupGroupingKeyAllocator");
        await Assert.That(source).Contains("ThreadConfinedSessionSlot");
        await Assert.That(source).Contains("PickupTagIdentity");
        await Assert.That(source).Contains("ClassifyUsingApplicableRequestedTagResolver");
        await Assert.That(source).Contains("originalTagBitsHash");
        await Assert.That(source).Contains("allocatedGroupingKey");
        await Assert.That(source).Contains("PickupGroupingKeyConstructor");
        await Assert.That(source).Contains("PickupablePrefabIdentityField");
        await Assert.That(source.Contains("[HarmonyPatch", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("AccessTools", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("Type.GetType", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("Assembly.GetAssemblies", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("FileVersionInfo", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("SHA256", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("operand.ToString", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("GetComponent", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("FastTrackOptions", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("Unpatch", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task AcceptedPickupHook_WhenInspected_UsesCachedManagedFieldsAndNoPerCandidateDiscovery()
    {
        string source = File.ReadAllText(ResolveProductionSourcePath(
            "PickupGroupingAdapters",
            "FastTrackPickupTemperaturePatches.cs"));
        int hookStartIndex = source.IndexOf(
            "private static int AllocatePickupGroupingKey(",
            StringComparison.Ordinal);
        int nextMethodIndex = source.IndexOf(
            "private static",
            hookStartIndex + 1,
            StringComparison.Ordinal);

        await Assert.That(hookStartIndex).IsGreaterThanOrEqualTo(0);
        await Assert.That(nextMethodIndex > hookStartIndex).IsTrue();
        string hookSource = source.Substring(
            hookStartIndex,
            nextMethodIndex - hookStartIndex);
        await Assert.That(hookSource).Contains("PrimaryElement primaryElement = pickupable.PrimaryElement");
        await Assert.That(hookSource).Contains("primaryElement!.InternalTemperature");
        await Assert.That(hookSource).Contains("kPrefabId.InstanceID");
        await Assert.That(hookSource).Contains("kPrefabId.PrefabTag");
        await Assert.That(hookSource.Contains(".Temperature", StringComparison.Ordinal)).IsFalse();
        await Assert.That(hookSource.Contains("TryCaptureCurrent", StringComparison.Ordinal)).IsFalse();
        await Assert.That(hookSource.Contains("CaptureSnapshot", StringComparison.Ordinal)).IsFalse();
        await Assert.That(hookSource.Contains("TryGetCurrent", StringComparison.Ordinal)).IsFalse();
        await Assert.That(hookSource.Contains("GetComponent", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task AdapterLifecycleSource_WhenInspected_CompletesAndDiscardsBothOwnedSessions()
    {
        string source = File.ReadAllText(ResolveProductionSourcePath(
            "PickupGroupingAdapters",
            "FastTrackPickupTemperaturePatches.cs"));

        await Assert.That(source).Contains("GroupingSession.Complete()");
        await Assert.That(source).Contains("GroupingKeyAllocator.Complete()");
        await Assert.That(source).Contains("GroupingSession.Discard()");
        await Assert.That(source).Contains("GroupingKeyAllocator.Discard()");
        await Assert.That(source).Contains("SessionScopeToken");
        await Assert.That(source).Contains("return __exception;");
        await Assert.That(source).Contains("RuntimeFailureReporting.DisableGameplay(nameof(BeforeUpdatePickupsFinalizer), cleanupException)");
        await Assert.That(source.Contains("return __exception ?? cleanupException", StringComparison.Ordinal)).IsFalse().Because("Mod cleanup failures must disable enforcement instead of becoming game exceptions.");
    }

    private static string ResolveProductionSourcePath(
        string semanticDirectoryName,
        string sourceFileName)
    {
        string? configuredRepositoryRoot = Environment.GetEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        string repositoryRoot = string.IsNullOrWhiteSpace(
            configuredRepositoryRoot)
            ? FindRepositoryRoot(AppContext.BaseDirectory)
            : Path.GetFullPath(configuredRepositoryRoot);
        return Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled",
            "Source",
            "FastTrackCompatibility",
            semanticDirectoryName,
            sourceFileName);
    }

    private static string FindRepositoryRoot(string startingDirectory)
    {
        for (DirectoryInfo? candidate =
                 new DirectoryInfo(Path.GetFullPath(startingDirectory));
             candidate != null;
             candidate = candidate.Parent)
        {
            if (Directory.Exists(Path.Combine(
                    candidate.FullName,
                    "mods",
                    "delivery-temperature-limit-supercooled",
                    "Source")))
            {
                return candidate.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the oxygen-not-included repository root from " +
            startingDirectory +
            ".");
    }
}
