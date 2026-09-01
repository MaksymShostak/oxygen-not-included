namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;

[TestClass]
public sealed class FastTrackDirectDeliveryEligibilityPatchContractTests
{
    [TestMethod]
    public void AdapterSource_WhenInspected_BindsOnlyReadyComparatorAndNarrowsOnlySuccessReturn()
    {
        string sourcePath = ResolveProductionSourcePath(
            "DirectDeliveryEligibilityAdapters",
            "FastTrackDirectDeliveryEligibilityPatches.cs");
        Assert.IsTrue(
            File.Exists(sourcePath),
            $"Missing inactive FastTrack direct-delivery adapter {sourcePath}.");
        string source = File.ReadAllText(sourcePath);

        StringAssert.Contains(
            source,
            "BindVerifiedDirectDeliveryEligibilityFeature");
        StringAssert.Contains(
            source,
            "FastTrackFeature.DirectDeliveryEligibility");
        StringAssert.Contains(
            source,
            "FastTrackFeatureCompatibilityState.Ready");
        StringAssert.Contains(
            source,
            "ResolveChoreComparatorCheckFetchChoreTarget");
        StringAssert.Contains(source, "CheckFetchChoreTranspiler");
        StringAssert.Contains(source, "originalSuccessReturn");
        StringAssert.Contains(source, "IsPickupAllowedForFetchChore");
        StringAssert.Contains(source, "TryGetConstraint");
        StringAssert.Contains(source, "constraint.Allows(temperatureKelvin)");
        StringAssert.Contains(source, "PrimaryElement primaryElement");
        Assert.IsFalse(source.Contains("[HarmonyPatch", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("AccessTools", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("Type.GetType", StringComparison.Ordinal));
        Assert.IsFalse(
            source.Contains("Assembly.GetAssemblies", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("FastTrackOptions", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("Unpatch", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("GetComponent", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PreservedAssemblyContractSource_WhenInspected_ExercisesDirectReplacementAbsence()
    {
        string staticContractPath = ResolveTestSourcePath(
            "PreservedFastTrackAssemblyContractTests.cs");
        string staticContractSource = File.ReadAllText(staticContractPath);

        StringAssert.Contains(
            staticContractSource,
            "PreservedFixture_DirectDeliveryReplacementMatchesDeclaredPresence");
        StringAssert.Contains(
            staticContractSource,
            "PeterHan.FastTrack.GamePatches.ChoreComparator");
        StringAssert.Contains(
            staticContractSource,
            "GlobalChoreProvider_CollectChores_Patch");
    }

    private static string ResolveProductionSourcePath(
        string semanticDirectoryName,
        string sourceFileName) =>
        Path.Combine(
            ResolveRepositoryRoot(),
            "mods",
            "delivery-temperature-limit-supercooled",
            "Source",
            "FastTrackCompatibility",
            semanticDirectoryName,
            sourceFileName);

    private static string ResolveTestSourcePath(string sourceFileName) =>
        Path.Combine(
            ResolveRepositoryRoot(),
            "mods",
            "delivery-temperature-limit-supercooled",
            "Tests",
            "FastTrackCompatibility",
            sourceFileName);

    private static string ResolveRepositoryRoot()
    {
        string? configuredRepositoryRoot = Environment.GetEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        if (!string.IsNullOrWhiteSpace(configuredRepositoryRoot))
        {
            return Path.GetFullPath(configuredRepositoryRoot);
        }

        for (DirectoryInfo? candidate =
                 new DirectoryInfo(Path.GetFullPath(AppContext.BaseDirectory));
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
            "Could not locate the oxygen-not-included repository root.");
    }
}
