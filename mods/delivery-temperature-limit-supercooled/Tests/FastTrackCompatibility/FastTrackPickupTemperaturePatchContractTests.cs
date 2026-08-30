namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;

[TestClass]
public sealed class FastTrackPickupTemperaturePatchContractTests
{
    [TestMethod]
    public void AdapterSource_WhenInspected_BindsReadyFeatureAndChangesOnlyConstructorHashArgument()
    {
        string sourcePath = ResolveProductionSourcePath(
            "PickupGroupingAdapters",
            "FastTrackPickupTemperaturePatches.cs");
        Assert.IsTrue(
            File.Exists(sourcePath),
            $"Missing inactive FastTrack pickup adapter {sourcePath}.");
        string source = File.ReadAllText(sourcePath);

        StringAssert.Contains(source, "BindVerifiedPickupGroupingFeature");
        StringAssert.Contains(source, "FastTrackFeature.PickupGrouping");
        StringAssert.Contains(
            source,
            "FastTrackFeatureCompatibilityState.Ready");
        StringAssert.Contains(
            source,
            "ResolveFetchManagerBeforeUpdatePickupsTarget");
        StringAssert.Contains(source, "ResolvePickupTagDictionaryAddItemTarget");
        StringAssert.Contains(source, "BeforeUpdatePickupsPrefix");
        StringAssert.Contains(source, "BeforeUpdatePickupsPostfix");
        StringAssert.Contains(source, "BeforeUpdatePickupsFinalizer");
        StringAssert.Contains(source, "PickupTagDictionaryAddItemTranspiler");
        StringAssert.Contains(source, "PickupTemperatureGroupingSession");
        StringAssert.Contains(source, "FastTrackPickupGroupingKeyAllocator");
        StringAssert.Contains(source, "ThreadConfinedSessionSlot");
        StringAssert.Contains(source, "PickupTagIdentity");
        StringAssert.Contains(
            source,
            "ClassifyUsingApplicableRequestedTagResolver");
        StringAssert.Contains(source, "originalTagBitsHash");
        StringAssert.Contains(source, "allocatedGroupingKey");
        StringAssert.Contains(source, "PickupGroupingKeyConstructor");
        StringAssert.Contains(source, "PickupablePrefabIdentityField");
        Assert.IsFalse(source.Contains("[HarmonyPatch", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("AccessTools", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("Type.GetType", StringComparison.Ordinal));
        Assert.IsFalse(
            source.Contains("Assembly.GetAssemblies", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("FileVersionInfo", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("SHA256", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("operand.ToString", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("GetComponent", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("FastTrackOptions", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("Unpatch", StringComparison.Ordinal));
    }

    [TestMethod]
    public void AcceptedPickupHook_WhenInspected_UsesCachedManagedFieldsAndNoPerCandidateDiscovery()
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

        Assert.IsGreaterThanOrEqualTo(0, hookStartIndex);
        Assert.IsTrue(nextMethodIndex > hookStartIndex);
        string hookSource = source.Substring(
            hookStartIndex,
            nextMethodIndex - hookStartIndex);
        StringAssert.Contains(
            hookSource,
            "PrimaryElement primaryElement = pickupable.PrimaryElement");
        StringAssert.Contains(hookSource, "primaryElement!.InternalTemperature");
        StringAssert.Contains(hookSource, "kPrefabId.InstanceID");
        StringAssert.Contains(hookSource, "kPrefabId.PrefabTag");
        Assert.IsFalse(hookSource.Contains(".Temperature", StringComparison.Ordinal));
        Assert.IsFalse(hookSource.Contains("TryCaptureCurrent", StringComparison.Ordinal));
        Assert.IsFalse(hookSource.Contains("CaptureSnapshot", StringComparison.Ordinal));
        Assert.IsFalse(hookSource.Contains("TryGetCurrent", StringComparison.Ordinal));
        Assert.IsFalse(hookSource.Contains("GetComponent", StringComparison.Ordinal));
    }

    [TestMethod]
    public void AdapterLifecycleSource_WhenInspected_CompletesAndDiscardsBothOwnedSessions()
    {
        string source = File.ReadAllText(ResolveProductionSourcePath(
            "PickupGroupingAdapters",
            "FastTrackPickupTemperaturePatches.cs"));

        StringAssert.Contains(source, "GroupingSession.Complete()");
        StringAssert.Contains(source, "GroupingKeyAllocator.Complete()");
        StringAssert.Contains(source, "GroupingSession.Discard()");
        StringAssert.Contains(source, "GroupingKeyAllocator.Discard()");
        StringAssert.Contains(source, "SessionScopeToken");
        StringAssert.Contains(source, "return __exception ?? cleanupException");
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
