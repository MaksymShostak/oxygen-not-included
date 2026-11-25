#nullable enable

namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;

/// <summary>Expected features of the sole selected, independently measured release fixture.</summary>
internal sealed class FastTrackSupportedBuildFixtureExpectation
{
    internal static IReadOnlyList<FastTrackSupportedBuildFixtureExpectation> DeclaredFixtures { get; }
        = Array.AsReadOnly(new[] { new FastTrackSupportedBuildFixtureExpectation() });

    private FastTrackSupportedBuildFixtureExpectation() { }

    internal FastTrackAssemblyBuildIdentity AssemblyBuildIdentity => FastTrackSupportedAssemblyBuildCatalog.SelectedBuild;
    internal string ExpectedAssemblyName => "FastTrack";
    internal string RelativeFixtureDirectoryPath => Path.Combine(AssemblyBuildIdentity.FileVersion.ToString(),
        "sha256-" + AssemblyBuildIdentity.AssemblySha256.ToLowerInvariant());

    internal bool WorldInventoryReplacementIsPresent => true;
    internal bool PickupGroupingReplacementIsPresent => true;
    internal bool DirectDeliveryReplacementIsPresent => false;
}
