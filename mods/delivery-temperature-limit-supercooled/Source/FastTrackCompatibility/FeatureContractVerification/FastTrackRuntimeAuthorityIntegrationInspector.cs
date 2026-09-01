#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Projects the verified FastTrack deep module into the provider-neutral
    /// declared runtime-authority model without retaining third-party objects.
    /// </summary>
    internal sealed class FastTrackRuntimeAuthorityIntegrationInspector :
        IRuntimeAuthorityIntegrationInspector
    {
        internal static DeclaredModIntegrationDescriptor
            DeclaredIntegrationDescriptor { get; } =
            new DeclaredModIntegrationDescriptor(
                new DeclaredModIntegrationId("fast-track"),
                "Fast Track",
                new[] { "PeterHan.FastTrack" },
                new[] { "FastTrack" },
                "https://github.com/peterhaneve/ONIMods/releases/tag/FastTrackBeta",
                new[]
                {
                    new DeclaredModIntegrationCapability(
                        RuntimeCapabilityId
                            .WorldInventoryTemperaturePublication,
                        ExternalModIntegrationCategory
                            .ExclusiveRuntimeAuthority),
                    new DeclaredModIntegrationCapability(
                        RuntimeCapabilityId.PickupTemperatureGrouping,
                        ExternalModIntegrationCategory
                            .ExclusiveRuntimeAuthority),
                    new DeclaredModIntegrationCapability(
                        RuntimeCapabilityId.DirectDeliveryEligibility,
                        ExternalModIntegrationCategory
                            .ExclusiveRuntimeAuthority)
                });

        private readonly FastTrackCompatibilityInspector compatibilityInspector;
        private readonly IFastTrackRuntimeAuthorityContributionBuilder
            contributionBuilder;

        internal FastTrackRuntimeAuthorityIntegrationInspector(
            FastTrackCompatibilityInspector compatibilityInspector,
            IFastTrackRuntimeAuthorityContributionBuilder contributionBuilder)
        {
            this.compatibilityInspector = compatibilityInspector ??
                throw new ArgumentNullException(nameof(compatibilityInspector));
            this.contributionBuilder = contributionBuilder ??
                throw new ArgumentNullException(nameof(contributionBuilder));
        }

        public DeclaredModIntegrationId IntegrationId =>
            DeclaredIntegrationDescriptor.IntegrationId;

        public PreparedRuntimeAuthorityInspection Inspect(
            DeclaredModIntegrationDescriptor descriptor,
            LoadedModInspectionContext context)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            DeclaredLoadedModMatch match = context.Match(descriptor);
            Assembly fastTrackAssembly = match.MatchedAssembly ??
                throw new InvalidOperationException(
                    "FastTrack runtime-authority inspection requires one exact " +
                    "same-entry assembly match.");
            var inspectionInput = new FastTrackLoadedGameInspectionInput(
                isFastTrackEnabledForLoadedGame: true,
                fastTrackAssembly,
                context.ActiveHarmonyPrefixes);
            FastTrackCompatibilityReport compatibilityReport =
                compatibilityInspector.Inspect(inspectionInput);

            var capabilityOutcomes = new List<
                ExternalModIntegrationCapabilityOutcome>(3);
            var contributions = new List<
                PreparedRuntimeAuthorityContribution>(3);
            var diagnostics = new List<ExternalModIntegrationDiagnostic>(3);

            ProjectFeature(
                descriptor.IntegrationId,
                RuntimeCapabilityId.WorldInventoryTemperaturePublication,
                compatibilityReport.GetFeature(FastTrackFeature.WorldInventory),
                context.ActiveHarmonyPrefixes,
                capabilityOutcomes,
                contributions,
                diagnostics);
            ProjectFeature(
                descriptor.IntegrationId,
                RuntimeCapabilityId.PickupTemperatureGrouping,
                compatibilityReport.GetFeature(FastTrackFeature.PickupGrouping),
                context.ActiveHarmonyPrefixes,
                capabilityOutcomes,
                contributions,
                diagnostics);
            ProjectFeature(
                descriptor.IntegrationId,
                RuntimeCapabilityId.DirectDeliveryEligibility,
                compatibilityReport.GetFeature(
                    FastTrackFeature.DirectDeliveryEligibility),
                context.ActiveHarmonyPrefixes,
                capabilityOutcomes,
                contributions,
                diagnostics);

            var outcome = new ExternalModIntegrationOutcome(
                descriptor.IntegrationId,
                descriptor.DisplayName,
                new[]
                {
                    ExternalModIntegrationCategory.ExclusiveRuntimeAuthority
                },
                DeclaredModMatchState.Matched,
                compatibilityReport.AssemblyIdentity,
                compatibilityReport.AssemblyVersion?.ToString(),
                compatibilityReport.FileVersion?.ToString(),
                NormalizeOptionalAssemblySha256(
                    compatibilityReport.AssemblySha256),
                capabilityOutcomes,
                diagnostics);
            return new PreparedRuntimeAuthorityInspection(
                outcome,
                contributions);
        }

        private void ProjectFeature(
            DeclaredModIntegrationId integrationId,
            RuntimeCapabilityId capabilityId,
            FastTrackFeatureCompatibility compatibility,
            IReadOnlyList<ActiveHarmonyPrefixDescriptor> activeHarmonyPrefixes,
            ICollection<ExternalModIntegrationCapabilityOutcome>
                capabilityOutcomes,
            ICollection<PreparedRuntimeAuthorityContribution> contributions,
            ICollection<ExternalModIntegrationDiagnostic> diagnostics)
        {
            switch (compatibility.State)
            {
                case FastTrackFeatureCompatibilityState.ModNotLoaded:
                case FastTrackFeatureCompatibilityState.ReplacementInactive:
                    capabilityOutcomes.Add(
                        CreateCapabilityOutcome(
                            capabilityId,
                            RuntimeAuthorityObservation.DoesNotOwn,
                            IntegrationContractState.NotEvaluated,
                            IntegrationCapabilityDisposition.NotApplicable,
                            diagnosticCode: null,
                            diagnosticMessage: null));
                    return;

                case FastTrackFeatureCompatibilityState.Ready:
                    PreparedRuntimeAuthorityContribution contribution =
                        contributionBuilder.Build(
                            integrationId,
                            capabilityId,
                            compatibility,
                            activeHarmonyPrefixes);
                    contributions.Add(contribution);
                    capabilityOutcomes.Add(
                        CreateCapabilityOutcome(
                            capabilityId,
                            RuntimeAuthorityObservation.OwnsCompatible,
                            IntegrationContractState.Compatible,
                            IntegrationCapabilityDisposition.Ready,
                            diagnosticCode: null,
                            diagnosticMessage: null));
                    return;

                case FastTrackFeatureCompatibilityState.Incompatible:
                    string diagnosticCode = GetDiagnosticCode(compatibility);
                    string diagnosticMessage = compatibility.FailureMessage ??
                        throw new InvalidOperationException(
                            "An incompatible FastTrack feature requires its " +
                            "verified failure message.");
                    contributions.Add(
                        new PreparedRuntimeAuthorityContribution(
                            RuntimeAuthorityImplementationIdentity
                                .ForDeclaredExternalIntegration(integrationId),
                            capabilityId,
                            Array.Empty<RuntimePatchGroupId>(),
                            RuntimeAuthorityObservation.OwnsIncompatible,
                            Array.Empty<HarmonyPatchContractBinding>(),
                            Array.Empty<RuntimeAuthorityRequirement>(),
                            diagnosticCode,
                            diagnosticMessage));
                    capabilityOutcomes.Add(
                        CreateCapabilityOutcome(
                            capabilityId,
                            RuntimeAuthorityObservation.OwnsIncompatible,
                            IntegrationContractState.Incompatible,
                            IntegrationCapabilityDisposition.Unavailable,
                            diagnosticCode,
                            diagnosticMessage));
                    diagnostics.Add(
                        new ExternalModIntegrationDiagnostic(
                            diagnosticCode,
                            diagnosticMessage));
                    return;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(compatibility),
                        compatibility.State,
                        "Unknown FastTrack feature compatibility state.");
            }
        }

        private static ExternalModIntegrationCapabilityOutcome
            CreateCapabilityOutcome(
                RuntimeCapabilityId capabilityId,
                RuntimeAuthorityObservation authorityObservation,
                IntegrationContractState contractState,
                IntegrationCapabilityDisposition disposition,
                string? diagnosticCode,
                string? diagnosticMessage) =>
            new ExternalModIntegrationCapabilityOutcome(
                capabilityId,
                ExternalModIntegrationCategory.ExclusiveRuntimeAuthority,
                authorityObservation,
                contractState,
                disposition,
                diagnosticCode,
                diagnosticMessage);

        private static string GetDiagnosticCode(
            FastTrackFeatureCompatibility compatibility)
        {
            FastTrackFeatureCompatibilityFailureCode failureCode =
                compatibility.FailureCode ??
                throw new InvalidOperationException(
                    "An incompatible FastTrack feature requires its stable " +
                    "failure code.");
            switch (compatibility.Feature)
            {
                case FastTrackFeature.WorldInventory:
                    return GetWorldInventoryDiagnosticCode(failureCode);
                case FastTrackFeature.PickupGrouping:
                    return GetPickupGroupingDiagnosticCode(failureCode);
                case FastTrackFeature.DirectDeliveryEligibility:
                    return GetDirectDeliveryDiagnosticCode(failureCode);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(compatibility),
                        compatibility.Feature,
                        "Unknown FastTrack feature.");
            }
        }

        private static string GetWorldInventoryDiagnosticCode(
            FastTrackFeatureCompatibilityFailureCode failureCode)
        {
            switch (failureCode)
            {
                case FastTrackFeatureCompatibilityFailureCode
                    .AssemblyFileIdentityUnavailable:
                    return "fast-track-world-inventory-identity-unavailable";
                case FastTrackFeatureCompatibilityFailureCode
                    .UnsupportedAssemblyBuild:
                    return "fast-track-world-inventory-build-unsupported";
                case FastTrackFeatureCompatibilityFailureCode
                    .WorldInventoryContractViolation:
                    return "fast-track-world-inventory-contract-incompatible";
                default:
                    throw UnexpectedFailureCode(
                        FastTrackFeature.WorldInventory,
                        failureCode);
            }
        }

        private static string GetPickupGroupingDiagnosticCode(
            FastTrackFeatureCompatibilityFailureCode failureCode)
        {
            switch (failureCode)
            {
                case FastTrackFeatureCompatibilityFailureCode
                    .AssemblyFileIdentityUnavailable:
                    return "fast-track-pickup-grouping-identity-unavailable";
                case FastTrackFeatureCompatibilityFailureCode
                    .UnsupportedAssemblyBuild:
                    return "fast-track-pickup-grouping-build-unsupported";
                case FastTrackFeatureCompatibilityFailureCode
                    .PickupGroupingContractViolation:
                    return "fast-track-pickup-grouping-contract-incompatible";
                default:
                    throw UnexpectedFailureCode(
                        FastTrackFeature.PickupGrouping,
                        failureCode);
            }
        }

        private static string GetDirectDeliveryDiagnosticCode(
            FastTrackFeatureCompatibilityFailureCode failureCode)
        {
            switch (failureCode)
            {
                case FastTrackFeatureCompatibilityFailureCode
                    .AssemblyFileIdentityUnavailable:
                    return "fast-track-direct-delivery-identity-unavailable";
                case FastTrackFeatureCompatibilityFailureCode
                    .UnsupportedAssemblyBuild:
                    return "fast-track-direct-delivery-build-unsupported";
                case FastTrackFeatureCompatibilityFailureCode
                    .DirectDeliveryEligibilityContractViolation:
                    return "fast-track-direct-delivery-contract-incompatible";
                default:
                    throw UnexpectedFailureCode(
                        FastTrackFeature.DirectDeliveryEligibility,
                        failureCode);
            }
        }

        private static ArgumentOutOfRangeException UnexpectedFailureCode(
            FastTrackFeature feature,
            FastTrackFeatureCompatibilityFailureCode failureCode) =>
            new ArgumentOutOfRangeException(
                nameof(failureCode),
                failureCode,
                "FastTrack feature " + feature +
                " reported a failure code owned by a different feature.");

        private static string? NormalizeOptionalAssemblySha256(string? value) =>
            value?.ToUpperInvariant();
    }
}
