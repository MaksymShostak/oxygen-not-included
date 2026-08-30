#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Owns the immutable, responsibility-ordered runtime implementation choice
    /// for one loaded game.
    /// </summary>
    /// <remarks>
    /// Content mode is intentionally absent: base-game and Spaced Out content use
    /// the same selection rules. An active incompatible delivery replacement is
    /// never converted into a Klei fallback because FastTrack would still own and
    /// suppress the original game method.
    /// </remarks>
    internal sealed class DeliveryTemperatureRuntimePatchPlan
    {
        private static readonly DeliveryTemperatureRuntimePatchGroup[]
            ContractOrderedPatchGroups =
            {
                DeliveryTemperatureRuntimePatchGroup.GameSessionLifecycle,
                DeliveryTemperatureRuntimePatchGroup.WorldParentTopology,
                DeliveryTemperatureRuntimePatchGroup
                    .KleiAuthoritativeFetchTemperatureEligibility,
                DeliveryTemperatureRuntimePatchGroup
                    .KleiWorldInventoryTemperaturePublication,
                DeliveryTemperatureRuntimePatchGroup
                    .FastTrackWorldInventoryTemperaturePublication,
                DeliveryTemperatureRuntimePatchGroup
                    .TemperatureStatusAvailability,
                DeliveryTemperatureRuntimePatchGroup
                    .KleiPickupTemperatureGrouping,
                DeliveryTemperatureRuntimePatchGroup
                    .FastTrackPickupTemperatureGrouping,
                DeliveryTemperatureRuntimePatchGroup
                    .KleiDirectDeliveryEligibility,
                DeliveryTemperatureRuntimePatchGroup
                    .FastTrackDirectDeliveryEligibility
            };

        private DeliveryTemperatureRuntimePatchPlan(
            IReadOnlyList<DeliveryTemperatureRuntimePatchGroup>
                orderedPatchGroups,
            string? statusCompatibilityDiagnostic)
        {
            OrderedPatchGroups = orderedPatchGroups;
            StatusCompatibilityDiagnostic = statusCompatibilityDiagnostic;
        }

        internal IReadOnlyList<DeliveryTemperatureRuntimePatchGroup>
            OrderedPatchGroups { get; }

        /// <summary>
        /// Explains why optional temperature-aware status integration was omitted
        /// while delivery correctness remained coherent. Null means no status-only
        /// compatibility degradation occurred.
        /// </summary>
        internal string? StatusCompatibilityDiagnostic { get; }

        internal static DeliveryTemperatureRuntimePatchPlan Create(
            bool checkTemperatureForStatusItems,
            FastTrackCompatibilityReport fastTrackCompatibility)
        {
            if (fastTrackCompatibility == null)
            {
                throw new ArgumentNullException(nameof(fastTrackCompatibility));
            }

            FastTrackFeatureCompatibility worldInventory =
                fastTrackCompatibility.GetFeature(
                    FastTrackFeature.WorldInventory);
            FastTrackFeatureCompatibility pickupGrouping =
                fastTrackCompatibility.GetFeature(
                    FastTrackFeature.PickupGrouping);
            FastTrackFeatureCompatibility directDeliveryEligibility =
                fastTrackCompatibility.GetFeature(
                    FastTrackFeature.DirectDeliveryEligibility);

            ThrowWhenActiveDeliveryFeatureIsIncompatible(
                pickupGrouping,
                fastTrackCompatibility);
            ThrowWhenActiveDeliveryFeatureIsIncompatible(
                directDeliveryEligibility,
                fastTrackCompatibility);

            string? statusCompatibilityDiagnostic =
                checkTemperatureForStatusItems &&
                worldInventory.State ==
                    FastTrackFeatureCompatibilityState.Incompatible
                ? CreateStatusCompatibilityDiagnostic(
                    worldInventory,
                    fastTrackCompatibility)
                : null;
            var selectedPatchGroups =
                new List<DeliveryTemperatureRuntimePatchGroup>(
                    ContractOrderedPatchGroups.Length);
            for (var groupIndex = 0;
                 groupIndex < ContractOrderedPatchGroups.Length;
                 groupIndex++)
            {
                DeliveryTemperatureRuntimePatchGroup patchGroup =
                    ContractOrderedPatchGroups[groupIndex];
                if (ShouldSelect(
                        patchGroup,
                        checkTemperatureForStatusItems,
                        worldInventory,
                        pickupGrouping,
                        directDeliveryEligibility))
                {
                    selectedPatchGroups.Add(patchGroup);
                }
            }

            ValidateSelectedResponsibilities(
                selectedPatchGroups,
                checkTemperatureForStatusItems,
                worldInventory.State,
                pickupGrouping.State,
                directDeliveryEligibility.State,
                statusCompatibilityDiagnostic);
            return new DeliveryTemperatureRuntimePatchPlan(
                new ReadOnlyCollection<DeliveryTemperatureRuntimePatchGroup>(
                    selectedPatchGroups),
                statusCompatibilityDiagnostic);
        }

        private static bool ShouldSelect(
            DeliveryTemperatureRuntimePatchGroup patchGroup,
            bool checkTemperatureForStatusItems,
            FastTrackFeatureCompatibility worldInventory,
            FastTrackFeatureCompatibility pickupGrouping,
            FastTrackFeatureCompatibility directDeliveryEligibility)
        {
            switch (patchGroup)
            {
                case DeliveryTemperatureRuntimePatchGroup.GameSessionLifecycle:
                case DeliveryTemperatureRuntimePatchGroup.WorldParentTopology:
                case DeliveryTemperatureRuntimePatchGroup
                    .KleiAuthoritativeFetchTemperatureEligibility:
                    return true;

                case DeliveryTemperatureRuntimePatchGroup
                    .KleiWorldInventoryTemperaturePublication:
                    return checkTemperatureForStatusItems &&
                        UsesKleiImplementation(worldInventory.State);

                case DeliveryTemperatureRuntimePatchGroup
                    .FastTrackWorldInventoryTemperaturePublication:
                    return checkTemperatureForStatusItems &&
                        worldInventory.State ==
                            FastTrackFeatureCompatibilityState.Ready;

                case DeliveryTemperatureRuntimePatchGroup
                    .TemperatureStatusAvailability:
                    return checkTemperatureForStatusItems &&
                        worldInventory.State !=
                            FastTrackFeatureCompatibilityState.Incompatible;

                case DeliveryTemperatureRuntimePatchGroup
                    .KleiPickupTemperatureGrouping:
                    return UsesKleiImplementation(pickupGrouping.State);

                case DeliveryTemperatureRuntimePatchGroup
                    .FastTrackPickupTemperatureGrouping:
                    return pickupGrouping.State ==
                        FastTrackFeatureCompatibilityState.Ready;

                case DeliveryTemperatureRuntimePatchGroup
                    .KleiDirectDeliveryEligibility:
                    return UsesKleiImplementation(
                        directDeliveryEligibility.State);

                case DeliveryTemperatureRuntimePatchGroup
                    .FastTrackDirectDeliveryEligibility:
                    return directDeliveryEligibility.State ==
                        FastTrackFeatureCompatibilityState.Ready;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(patchGroup),
                        patchGroup,
                        "Unknown delivery-temperature runtime patch group.");
            }
        }

        private static bool UsesKleiImplementation(
            FastTrackFeatureCompatibilityState compatibilityState)
        {
            switch (compatibilityState)
            {
                case FastTrackFeatureCompatibilityState.ModNotLoaded:
                case FastTrackFeatureCompatibilityState.ReplacementInactive:
                    return true;
                case FastTrackFeatureCompatibilityState.Ready:
                case FastTrackFeatureCompatibilityState.Incompatible:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(compatibilityState),
                        compatibilityState,
                        "Unknown FastTrack compatibility state.");
            }
        }

        private static void ThrowWhenActiveDeliveryFeatureIsIncompatible(
            FastTrackFeatureCompatibility deliveryFeature,
            FastTrackCompatibilityReport compatibilityReport)
        {
            if (deliveryFeature.State !=
                FastTrackFeatureCompatibilityState.Incompatible)
            {
                return;
            }

            throw new FastTrackDeliveryEligibilityCompatibilityException(
                "Delivery Temperature Limit cannot activate because the active " +
                "FastTrack " +
                deliveryFeature.Feature +
                " replacement is incompatible. " +
                CreateCompatibilityEvidence(
                    deliveryFeature,
                    compatibilityReport),
                compatibilityReport);
        }

        private static string CreateStatusCompatibilityDiagnostic(
            FastTrackFeatureCompatibility worldInventory,
            FastTrackCompatibilityReport compatibilityReport) =>
            "Temperature-aware resource-status integration is disabled for " +
            "this loaded game; existing ONI status availability remains " +
            "unchanged. " +
            CreateCompatibilityEvidence(
                worldInventory,
                compatibilityReport);

        private static string CreateCompatibilityEvidence(
            FastTrackFeatureCompatibility feature,
            FastTrackCompatibilityReport report) =>
            "Feature " +
            feature.Feature +
            "; assembly identity " +
            FormatOptional(report.AssemblyIdentity) +
            "; assembly version " +
            FormatOptional(report.AssemblyVersion) +
            "; file version " +
            FormatOptional(report.FileVersion) +
            "; SHA-256 " +
            FormatOptional(report.AssemblySha256) +
            "; failure code " +
            FormatOptional(feature.FailureCode) +
            "; structural failure: " +
            FormatOptional(feature.FailureMessage) +
            ". FastTrack file version 0.18.4.0 support is best-efforts and " +
            "applies only to that verified version and member shape.";

        private static string FormatOptional(object? value) =>
            value == null
                ? "<unavailable>"
                : value.ToString() ?? "<unavailable>";

        private static void ValidateSelectedResponsibilities(
            IReadOnlyList<DeliveryTemperatureRuntimePatchGroup> groups,
            bool checkTemperatureForStatusItems,
            FastTrackFeatureCompatibilityState worldInventoryState,
            FastTrackFeatureCompatibilityState pickupGroupingState,
            FastTrackFeatureCompatibilityState directDeliveryState,
            string? statusCompatibilityDiagnostic)
        {
            if (groups.Count < 5 ||
                groups[0] !=
                    DeliveryTemperatureRuntimePatchGroup.GameSessionLifecycle ||
                groups[1] !=
                    DeliveryTemperatureRuntimePatchGroup.WorldParentTopology ||
                groups[2] != DeliveryTemperatureRuntimePatchGroup
                    .KleiAuthoritativeFetchTemperatureEligibility)
            {
                throw new InvalidOperationException(
                    "A runtime patch plan must begin with lifecycle, topology, " +
                    "and authoritative fetch eligibility in contract order.");
            }

            int inventoryGroupCount = CountSelected(
                groups,
                DeliveryTemperatureRuntimePatchGroup
                    .KleiWorldInventoryTemperaturePublication,
                DeliveryTemperatureRuntimePatchGroup
                    .FastTrackWorldInventoryTemperaturePublication);
            int statusGroupCount = Contains(
                groups,
                DeliveryTemperatureRuntimePatchGroup
                    .TemperatureStatusAvailability)
                ? 1
                : 0;
            bool compatibleStatusWasRequested =
                checkTemperatureForStatusItems &&
                worldInventoryState !=
                    FastTrackFeatureCompatibilityState.Incompatible;
            if (inventoryGroupCount !=
                    (compatibleStatusWasRequested ? 1 : 0) ||
                statusGroupCount !=
                    (compatibleStatusWasRequested ? 1 : 0) ||
                (statusCompatibilityDiagnostic != null) !=
                    (checkTemperatureForStatusItems &&
                     worldInventoryState ==
                        FastTrackFeatureCompatibilityState.Incompatible))
            {
                throw new InvalidOperationException(
                    "Inventory publication, status instrumentation, and its " +
                    "compatibility diagnostic are not coherent.");
            }

            RequireExactlyOneSelectedImplementation(
                groups,
                pickupGroupingState,
                DeliveryTemperatureRuntimePatchGroup
                    .KleiPickupTemperatureGrouping,
                DeliveryTemperatureRuntimePatchGroup
                    .FastTrackPickupTemperatureGrouping,
                "pickup grouping");
            RequireExactlyOneSelectedImplementation(
                groups,
                directDeliveryState,
                DeliveryTemperatureRuntimePatchGroup
                    .KleiDirectDeliveryEligibility,
                DeliveryTemperatureRuntimePatchGroup
                    .FastTrackDirectDeliveryEligibility,
                "direct-delivery eligibility");
        }

        private static void RequireExactlyOneSelectedImplementation(
            IReadOnlyList<DeliveryTemperatureRuntimePatchGroup> groups,
            FastTrackFeatureCompatibilityState featureState,
            DeliveryTemperatureRuntimePatchGroup kleiGroup,
            DeliveryTemperatureRuntimePatchGroup fastTrackGroup,
            string responsibility)
        {
            int selectedCount = CountSelected(
                groups,
                kleiGroup,
                fastTrackGroup);
            bool selectedExpectedGroup =
                featureState == FastTrackFeatureCompatibilityState.Ready
                    ? Contains(groups, fastTrackGroup)
                    : UsesKleiImplementation(featureState) &&
                        Contains(groups, kleiGroup);
            if (selectedCount != 1 || !selectedExpectedGroup)
            {
                throw new InvalidOperationException(
                    "A runtime patch plan must select exactly one verified " +
                    responsibility +
                    " implementation.");
            }
        }

        private static int CountSelected(
            IReadOnlyList<DeliveryTemperatureRuntimePatchGroup> groups,
            DeliveryTemperatureRuntimePatchGroup first,
            DeliveryTemperatureRuntimePatchGroup second) =>
            (Contains(groups, first) ? 1 : 0) +
            (Contains(groups, second) ? 1 : 0);

        private static bool Contains(
            IReadOnlyList<DeliveryTemperatureRuntimePatchGroup> groups,
            DeliveryTemperatureRuntimePatchGroup expected)
        {
            for (var groupIndex = 0;
                 groupIndex < groups.Count;
                 groupIndex++)
            {
                if (groups[groupIndex] == expected)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
