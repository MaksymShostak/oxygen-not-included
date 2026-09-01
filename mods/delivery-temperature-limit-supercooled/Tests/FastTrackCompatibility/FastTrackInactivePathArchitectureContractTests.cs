#nullable enable

using System.Reflection;
using DeliveryTemperatureLimit.Tests.RuntimePatchInstallation;

namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;

/// <summary>
/// Proves that a loaded game which does not have an active verified FastTrack
/// replacement receives direct Klei patch groups. The hot Klei methods are then
/// checked independently so the cold compatibility result cannot leak into an
/// update-time branch or adapter dispatch.
/// </summary>
[TestClass]
public sealed class FastTrackInactivePathArchitectureContractTests
{
    private const string SupportedDigest =
        "D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD";
    private const string UnsupportedDigest =
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    private static readonly string[]
        RequiredKleiGroupsWhenStatusIsEnabled =
        [
            "klei-authoritative-fetch-temperature-eligibility",
            "klei-world-inventory-temperature-publication",
            "temperature-status-availability",
            "klei-pickup-temperature-grouping",
            "klei-direct-delivery-eligibility"
        ];

    private static readonly string[]
        ProhibitedFastTrackGroups =
        [
            "fast-track-world-inventory-temperature-publication",
            "fast-track-pickup-temperature-grouping",
            "fast-track-direct-delivery-eligibility"
        ];

    [TestMethod]
    public void RuntimePlan_WhenFastTrackIsNotLoaded_SelectsKleiImplementationPathsDirectly()
    {
        var identityReader = new RecordingAssemblyIdentityReader();
        var inspector = new FastTrackCompatibilityInspector(
            identityReader,
            CreateSupportedTestCatalog());
        FastTrackCompatibilityReport report = inspector.Inspect(
            new FastTrackLoadedGameInspectionInput(
                isFastTrackEnabledForLoadedGame: false,
                fastTrackAssembly: null,
                activeHarmonyPrefixes: []));

        Assert.AreEqual(0, identityReader.ReadCallCount);
        AssertKleiImplementationPathsAreSelected(report);
    }

    [TestMethod]
    public void RuntimePlan_WhenFastTrackIsDisabledForLoadedGame_SelectsKleiWithoutInspectingAssembly()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();
        var identityReader = new RecordingAssemblyIdentityReader();
        var inspector = new FastTrackCompatibilityInspector(
            identityReader,
            CreateSupportedTestCatalog());
        FastTrackCompatibilityReport report = inspector.Inspect(
            new FastTrackLoadedGameInspectionInput(
                isFastTrackEnabledForLoadedGame: false,
                fixture.Assembly,
                fixture.AllReplacements.ToArray()));

        Assert.AreEqual(
            0,
            identityReader.ReadCallCount,
            "A disabled mod for the loaded game must not pay even the cold " +
            "physical-identity read cost.");
        AssertKleiImplementationPathsAreSelected(report);
    }

    [TestMethod]
    public void RuntimePlan_WhenFastTrackReplacementsAreInactive_SelectsKleiImplementationPathsDirectly()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();
        var identityReader = new RecordingAssemblyIdentityReader(
            new FastTrackAssemblyFileIdentity(
                FastTrackAssemblyFileIdentityReadState.Success,
                new Version(0, 18, 4, 0),
                UnsupportedDigest,
                failureMessage: null));
        var inspector = new FastTrackCompatibilityInspector(
            identityReader,
            CreateSupportedTestCatalog());
        FastTrackCompatibilityReport report = inspector.Inspect(
            new FastTrackLoadedGameInspectionInput(
                isFastTrackEnabledForLoadedGame: true,
                fixture.Assembly,
                activeHarmonyPrefixes: []));

        Assert.AreEqual(1, identityReader.ReadCallCount);
        foreach (FastTrackFeature feature in EnumerateFeatures())
        {
            Assert.AreEqual(
                FastTrackFeatureCompatibilityState.ReplacementInactive,
                report.GetFeature(feature).State);
        }

        AssertKleiImplementationPathsAreSelected(report);
    }

    [TestMethod]
    public void KleiImplementationPaths_WhenFastTrackIsAbsent_ReferenceNoFastTrackHotPathMethod()
    {
        var subjects = new[]
        {
            new HotPathSourceSubject(
                "KleiImplementationAdapters/KleiWorldInventoryTemperaturePatches.cs",
                "private static float RecordFilteredPickupTemperatureAmount("),
            new HotPathSourceSubject(
                "KleiImplementationAdapters/KleiPickupTemperatureGroupingPatches.cs",
                "private static int CompareTemperatureEligibilityClasses("),
            new HotPathSourceSubject(
                "KleiImplementationAdapters/KleiPickupTemperatureGroupingPatches.cs",
                "private static bool HaveSameTemperatureEligibilityClass("),
            new HotPathSourceSubject(
                "KleiImplementationAdapters/KleiDirectDeliveryEligibilityPatches.cs",
                "private static bool IsPickupAllowedForDestination("),
            new HotPathSourceSubject(
                "KleiImplementationAdapters/TemperatureStatusAvailabilityPatches.cs",
                "private static void ReplaceFetchableAmountWhenInventoryIsComplete(")
        };
        string[] prohibitedHotPathReferences =
        [
            nameof(FastTrackCompatibilityInspector),
            nameof(FastTrackWorldInventoryPublicationSession),
            nameof(FastTrackPickupGroupingKeyAllocator),
            "FastTrackVerifiedMember",
            "FastTrackCompatibilityReport",
            "PeterHan.FastTrack"
        ];

        foreach (HotPathSourceSubject subject in subjects)
        {
            string sourcePath = Path.Combine(
                ResolveSourceRoot(),
                subject.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            string methodBody = ExtractMethodBody(
                File.ReadAllText(sourcePath),
                subject.DeclarationMarker);
            foreach (string prohibitedReference in prohibitedHotPathReferences)
            {
                Assert.IsFalse(
                    methodBody.Contains(
                        prohibitedReference,
                        StringComparison.Ordinal),
                    $"Klei hot method {subject.DeclarationMarker} in " +
                    $"{sourcePath} references cold/compatibility member " +
                    $"{prohibitedReference}.");
            }
        }
    }

    private static void AssertKleiImplementationPathsAreSelected(
        FastTrackCompatibilityReport report)
    {
        foreach (FastTrackFeature feature in EnumerateFeatures())
        {
            FastTrackFeatureCompatibilityState state =
                report.GetFeature(feature).State;
            Assert.IsTrue(
                state == FastTrackFeatureCompatibilityState.ModNotLoaded ||
                state ==
                    FastTrackFeatureCompatibilityState.ReplacementInactive,
                $"Inactive-path fixture unexpectedly observed {feature} as " +
                $"{state}.");
        }

        DeliveryTemperatureRuntimePatchPlan plan =
            DeliveryTemperatureRuntimePatchPlan.Create(
                checkTemperatureForStatusItems: true,
                RuntimePatchCapabilitySelectionFixture
                    .CreateKleiBaselineSelection());

        foreach (string requiredGroup in
                 RequiredKleiGroupsWhenStatusIsEnabled)
        {
            Assert.IsTrue(
                plan.OrderedPatchGroupIds.Any(group => string.Equals(
                    group.Value,
                    requiredGroup,
                    StringComparison.Ordinal)),
                $"The inactive FastTrack plan omitted required Klei group " +
                $"{requiredGroup}.");
        }

        foreach (string prohibitedGroup in
                 ProhibitedFastTrackGroups)
        {
            Assert.IsFalse(
                plan.OrderedPatchGroupIds.Any(group => string.Equals(
                    group.Value,
                    prohibitedGroup,
                    StringComparison.Ordinal)),
                $"The inactive FastTrack plan selected prohibited group " +
                $"{prohibitedGroup}.");
        }
    }

    private static IEnumerable<FastTrackFeature> EnumerateFeatures()
    {
        yield return FastTrackFeature.WorldInventory;
        yield return FastTrackFeature.PickupGrouping;
        yield return FastTrackFeature.DirectDeliveryEligibility;
    }

    private static FastTrackSupportedAssemblyBuildCatalog
        CreateSupportedTestCatalog() =>
        new FastTrackSupportedAssemblyBuildCatalog(new[]
        {
            new FastTrackAssemblyBuildIdentity(
                new Version(0, 18, 4, 0),
                SupportedDigest)
        });

    private static string ExtractMethodBody(
        string source,
        string declarationMarker)
    {
        int declarationIndex = source.IndexOf(
            declarationMarker,
            StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(
            0,
            declarationIndex,
            $"Missing exact hot-method declaration {declarationMarker}.");
        Assert.AreEqual(
            declarationIndex,
            source.LastIndexOf(declarationMarker, StringComparison.Ordinal),
            $"Hot-method declaration is not unique: {declarationMarker}.");
        int openingBraceIndex = source.IndexOf(
            '{',
            declarationIndex + declarationMarker.Length);
        Assert.IsGreaterThanOrEqualTo(
            0,
            openingBraceIndex,
            $"Hot-method declaration has no body: {declarationMarker}.");

        int nestingDepth = 0;
        for (int characterIndex = openingBraceIndex;
             characterIndex < source.Length;
             characterIndex++)
        {
            if (source[characterIndex] == '{')
            {
                nestingDepth++;
            }
            else if (source[characterIndex] == '}')
            {
                nestingDepth--;
                if (nestingDepth == 0)
                {
                    return source.Substring(
                        declarationIndex,
                        characterIndex - declarationIndex + 1);
                }
            }
        }

        Assert.Fail($"Hot-method body is unbalanced: {declarationMarker}.");
        return string.Empty;
    }

    private static string ResolveSourceRoot()
    {
        string? repositoryRoot = Environment.GetEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        if (!string.IsNullOrWhiteSpace(repositoryRoot))
        {
            return Path.Combine(
                repositoryRoot,
                "mods",
                "delivery-temperature-limit-supercooled",
                "Source");
        }

        DirectoryInfo? candidateDirectory = new(AppContext.BaseDirectory);
        while (candidateDirectory is not null)
        {
            string sourceRoot = Path.Combine(
                candidateDirectory.FullName,
                "mods",
                "delivery-temperature-limit-supercooled",
                "Source");
            if (File.Exists(Path.Combine(
                    sourceRoot,
                    "DeliveryTemperatureLimit.csproj")))
            {
                return sourceRoot;
            }

            candidateDirectory = candidateDirectory.Parent;
        }

        throw new InvalidOperationException(
            "The Delivery Temperature Limit source root could not be resolved.");
    }

    private sealed class RecordingAssemblyIdentityReader :
        IFastTrackAssemblyFileIdentityReader
    {
        private readonly FastTrackAssemblyFileIdentity? result;

        internal RecordingAssemblyIdentityReader(
            FastTrackAssemblyFileIdentity? result = null)
        {
            this.result = result;
        }

        internal int ReadCallCount { get; private set; }

        public FastTrackAssemblyFileIdentity Read(Assembly fastTrackAssembly)
        {
            ReadCallCount++;
            return result ?? throw new InvalidOperationException(
                "The physical identity reader must not run for this scenario.");
        }
    }

    private sealed record HotPathSourceSubject(
        string RelativePath,
        string DeclarationMarker);
}
