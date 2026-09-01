#nullable enable

using System.Collections.Generic;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Prepares the complete game-facing runtime authority contribution for one
    /// structurally verified FastTrack feature. The FastTrack inspector remains
    /// BCL-only while the production implementation resolves game patch bindings.
    /// </summary>
    internal interface IFastTrackRuntimeAuthorityContributionBuilder
    {
        PreparedRuntimeAuthorityContribution Build(
            DeclaredModIntegrationId integrationId,
            RuntimeCapabilityId capabilityId,
            FastTrackFeatureCompatibility readyFeature,
            IReadOnlyList<ActiveHarmonyPrefixDescriptor> activePrefixes);
    }
}
