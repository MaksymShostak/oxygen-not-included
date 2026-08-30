#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Publishes one immutable, game-load-scoped FastTrack compatibility result.
    /// Feature consumers use the explicit feature key so an inventory mismatch
    /// cannot accidentally disable or authorize a delivery replacement.
    /// </summary>
    internal sealed class FastTrackCompatibilityReport
    {
        private readonly FastTrackFeatureCompatibility worldInventory;
        private readonly FastTrackFeatureCompatibility pickupGrouping;
        private readonly FastTrackFeatureCompatibility directDeliveryEligibility;

        internal FastTrackCompatibilityReport(
            string? assemblyIdentity,
            Version? assemblyVersion,
            FastTrackAssemblyFileIdentityReadState assemblyFileIdentityReadState,
            Version? fileVersion,
            string? assemblySha256,
            FastTrackFeatureCompatibility worldInventory,
            FastTrackFeatureCompatibility pickupGrouping,
            FastTrackFeatureCompatibility directDeliveryEligibility)
        {
            this.worldInventory = RequireFeature(
                worldInventory,
                FastTrackFeature.WorldInventory,
                nameof(worldInventory));
            this.pickupGrouping = RequireFeature(
                pickupGrouping,
                FastTrackFeature.PickupGrouping,
                nameof(pickupGrouping));
            this.directDeliveryEligibility = RequireFeature(
                directDeliveryEligibility,
                FastTrackFeature.DirectDeliveryEligibility,
                nameof(directDeliveryEligibility));
            AssemblyIdentity = assemblyIdentity;
            AssemblyVersion = assemblyVersion;
            AssemblyFileIdentityReadState = assemblyFileIdentityReadState;
            FileVersion = fileVersion;
            AssemblySha256 = assemblySha256;
        }

        internal string? AssemblyIdentity { get; }

        internal Version? AssemblyVersion { get; }

        internal FastTrackAssemblyFileIdentityReadState
            AssemblyFileIdentityReadState { get; }

        internal Version? FileVersion { get; }

        internal string? AssemblySha256 { get; }

        internal FastTrackFeatureCompatibility GetFeature(
            FastTrackFeature feature)
        {
            switch (feature)
            {
                case FastTrackFeature.WorldInventory:
                    return worldInventory;
                case FastTrackFeature.PickupGrouping:
                    return pickupGrouping;
                case FastTrackFeature.DirectDeliveryEligibility:
                    return directDeliveryEligibility;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(feature),
                        feature,
                        "Unknown FastTrack feature.");
            }
        }

        private static FastTrackFeatureCompatibility RequireFeature(
            FastTrackFeatureCompatibility compatibility,
            FastTrackFeature requiredFeature,
            string parameterName)
        {
            if (compatibility == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (compatibility.Feature != requiredFeature)
            {
                throw new ArgumentException(
                    "The compatibility result must describe " +
                    requiredFeature +
                    ".",
                    parameterName);
            }

            return compatibility;
        }
    }
}
