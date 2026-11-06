namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;
public sealed class FastTrackDirectDeliveryEligibilityPatchContractTests
{
    [Test]
    public async Task AdapterSource_WhenInspected_BindsOnlyReadyComparatorAndNarrowsOnlySuccessReturn()
    {
        string sourcePath = ResolveProductionSourcePath(
            "DirectDeliveryEligibilityAdapters",
            "FastTrackDirectDeliveryEligibilityPatches.cs");
        await Assert.That(File.Exists(sourcePath)).IsTrue().Because($"Missing inactive FastTrack direct-delivery adapter {sourcePath}.");
        string source = File.ReadAllText(sourcePath);

        await Assert.That(source).Contains("BindVerifiedDirectDeliveryEligibilityFeature");
        await Assert.That(source).Contains("FastTrackFeature.DirectDeliveryEligibility");
        await Assert.That(source).Contains("FastTrackFeatureCompatibilityState.Ready");
        await Assert.That(source).Contains("ResolveChoreComparatorCheckFetchChoreTarget");
        await Assert.That(source).Contains("CheckFetchChoreTranspiler");
        await Assert.That(source).Contains("originalSuccessReturn");
        await Assert.That(source).Contains("IsPickupAllowedForFetchChore");
        await Assert.That(source).Contains("TryGetConstraint");
        await Assert.That(source).Contains("constraint.Allows(temperatureKelvin)");
        await Assert.That(source).Contains("PrimaryElement primaryElement");
        await Assert.That(source.Contains("[HarmonyPatch", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("AccessTools", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("Type.GetType", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("Assembly.GetAssemblies", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("FastTrackOptions", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("Unpatch", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("GetComponent", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task PreservedAssemblyContractSource_WhenInspected_ExercisesDirectReplacementAbsence()
    {
        string staticContractPath = ResolveTestSourcePath(
            "PreservedFastTrackAssemblyContractTests.cs");
        string staticContractSource = File.ReadAllText(staticContractPath);

        await Assert.That(staticContractSource).Contains("PreservedFixture_DirectDeliveryReplacementMatchesDeclaredPresence");
        await Assert.That(staticContractSource).Contains("PeterHan.FastTrack.GamePatches.ChoreComparator");
        await Assert.That(staticContractSource).Contains("GlobalChoreProvider_CollectChores_Patch");
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
