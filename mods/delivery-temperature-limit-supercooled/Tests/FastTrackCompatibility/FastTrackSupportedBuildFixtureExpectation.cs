#nullable enable

using System.Collections.ObjectModel;

namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;

/// <summary>
/// Declares the independently checked PE identity and feature contract for one
/// exact FastTrack build admitted by production policy.
/// </summary>
internal sealed class FastTrackSupportedBuildFixtureExpectation
{
    private static readonly IReadOnlyList<
        FastTrackSupportedBuildFixtureExpectation> DeclaredFixtureSet =
        new ReadOnlyCollection<FastTrackSupportedBuildFixtureExpectation>(
            new[]
            {
                new FastTrackSupportedBuildFixtureExpectation(
                    new FastTrackAssemblyBuildIdentity(
                        new Version(0, 18, 4, 0),
                        "D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD"),
                    new Version(0, 18, 0, 0),
                    new Guid("b1e31127-5b91-4607-b5b5-8ea255bd5288"),
                    worldInventoryReplacementIsPresent: true,
                    pickupGroupingReplacementIsPresent: true,
                    directDeliveryReplacementIsPresent: false),
                new FastTrackSupportedBuildFixtureExpectation(
                    new FastTrackAssemblyBuildIdentity(
                        new Version(0, 18, 5, 0),
                        "CDF0150546952FDA3A31A612D61FBEF3808E05DB89B9B6E8CCEEA1F3C752AA3B"),
                    new Version(0, 18, 0, 0),
                    new Guid("bb4e7a11-4985-4d8f-b1c9-f497c6bb3d1e"),
                    worldInventoryReplacementIsPresent: true,
                    pickupGroupingReplacementIsPresent: true,
                    directDeliveryReplacementIsPresent: false)
            });

    private FastTrackSupportedBuildFixtureExpectation(
        FastTrackAssemblyBuildIdentity assemblyBuildIdentity,
        Version expectedAssemblyVersion,
        Guid expectedModuleVersionId,
        bool worldInventoryReplacementIsPresent,
        bool pickupGroupingReplacementIsPresent,
        bool directDeliveryReplacementIsPresent)
    {
        AssemblyBuildIdentity = assemblyBuildIdentity ??
            throw new ArgumentNullException(nameof(assemblyBuildIdentity));
        ExpectedAssemblyVersion = expectedAssemblyVersion ??
            throw new ArgumentNullException(nameof(expectedAssemblyVersion));
        if (expectedModuleVersionId == Guid.Empty)
        {
            throw new ArgumentException(
                "The expected module version identifier must not be empty.",
                nameof(expectedModuleVersionId));
        }

        ExpectedModuleVersionId = expectedModuleVersionId;
        WorldInventoryReplacementIsPresent =
            worldInventoryReplacementIsPresent;
        PickupGroupingReplacementIsPresent =
            pickupGroupingReplacementIsPresent;
        DirectDeliveryReplacementIsPresent =
            directDeliveryReplacementIsPresent;
        RelativeFixtureDirectoryPath = Path.Combine(
            AssemblyBuildIdentity.FileVersion.ToString(),
            "sha256-" +
            AssemblyBuildIdentity.AssemblySha256.ToLowerInvariant());
    }

    internal static IReadOnlyList<FastTrackSupportedBuildFixtureExpectation>
        DeclaredFixtures => DeclaredFixtureSet;

    internal FastTrackAssemblyBuildIdentity AssemblyBuildIdentity { get; }

    internal string ExpectedAssemblyName => "FastTrack";

    internal Version ExpectedAssemblyVersion { get; }

    internal Guid ExpectedModuleVersionId { get; }

    internal string RelativeFixtureDirectoryPath { get; }

    internal bool WorldInventoryReplacementIsPresent { get; }

    internal bool PickupGroupingReplacementIsPresent { get; }

    internal bool DirectDeliveryReplacementIsPresent { get; }
}
