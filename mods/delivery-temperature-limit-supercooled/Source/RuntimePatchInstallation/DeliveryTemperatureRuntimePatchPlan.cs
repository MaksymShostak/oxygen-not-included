#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Owns one immutable provider-neutral runtime patch plan for the loaded
    /// game. Every selected contribution is complete before this plan exists.
    /// </summary>
    internal sealed class DeliveryTemperatureRuntimePatchPlan
    {
        private DeliveryTemperatureRuntimePatchPlan(
            IReadOnlyList<PreparedRuntimeAuthorityContribution>
                selectedContributions,
            IReadOnlyList<RuntimePatchGroupId> orderedPatchGroupIds,
            HarmonyPatchContractBindingVerifier.VerifiedBindings
                orderedPatchBindings,
            IReadOnlyList<RuntimeAuthorityRequirement>
                authorityRequirements,
            IReadOnlyList<ExternalModIntegrationOutcome>
                externalModIntegrationOutcomes,
            string? statusCompatibilityDiagnostic)
        {
            SelectedContributions = selectedContributions;
            OrderedPatchGroupIds = orderedPatchGroupIds;
            OrderedPatchBindings = orderedPatchBindings;
            AuthorityRequirements = authorityRequirements;
            ExternalModIntegrationOutcomes = externalModIntegrationOutcomes;
            StatusCompatibilityDiagnostic = statusCompatibilityDiagnostic;
        }

        internal IReadOnlyList<PreparedRuntimeAuthorityContribution>
            SelectedContributions { get; }

        internal IReadOnlyList<RuntimePatchGroupId> OrderedPatchGroupIds { get; }

        internal HarmonyPatchContractBindingVerifier.VerifiedBindings
            OrderedPatchBindings { get; }

        internal IReadOnlyList<RuntimeAuthorityRequirement>
            AuthorityRequirements { get; }

        internal IReadOnlyList<ExternalModIntegrationOutcome>
            ExternalModIntegrationOutcomes { get; }

        /// <summary>
        /// Explains why optional temperature-aware status integration was omitted
        /// while delivery correctness remained coherent. Null means no status-only
        /// compatibility degradation occurred.
        /// </summary>
        internal string? StatusCompatibilityDiagnostic { get; }

        internal static DeliveryTemperatureRuntimePatchPlan Create(
            bool checkTemperatureForStatusItems,
            RuntimePatchCapabilitySelection capabilitySelection)
        {
            if (capabilitySelection == null)
            {
                throw new ArgumentNullException(nameof(capabilitySelection));
            }

            RuntimeCapabilitySelectionEntry worldInventorySelection =
                capabilitySelection.GetCapabilitySelection(
                    RuntimeCapabilityId.WorldInventoryTemperaturePublication);
            RuntimeCapabilitySelectionEntry statusAvailabilitySelection =
                capabilitySelection.GetCapabilitySelection(
                    RuntimeCapabilityId.TemperatureStatusAvailability);
            bool selectStatusResponsibilities =
                checkTemperatureForStatusItems &&
                worldInventorySelection.HasSelectedContribution &&
                statusAvailabilitySelection.HasSelectedContribution;
            string? statusCompatibilityDiagnostic =
                checkTemperatureForStatusItems &&
                !selectStatusResponsibilities
                    ? CreateStatusCompatibilityDiagnostic(
                        !worldInventorySelection.HasSelectedContribution
                            ? worldInventorySelection
                            : statusAvailabilitySelection)
                    : null;

            var selectedContributions =
                new List<PreparedRuntimeAuthorityContribution>();
            for (int selectionIndex = 0;
                 selectionIndex <
                    capabilitySelection.CapabilitySelections.Count;
                 selectionIndex++)
            {
                RuntimeCapabilitySelectionEntry selection =
                    capabilitySelection.CapabilitySelections[selectionIndex];
                if (IsStatusResponsibility(selection.CapabilityId) &&
                    !selectStatusResponsibilities)
                {
                    continue;
                }

                if (selection.HasSelectedContribution)
                {
                    selectedContributions.Add(
                        selection.PrepareSelectedContribution());
                }
            }

            IReadOnlyList<ExternalModIntegrationOutcome>
                externalModIntegrationOutcomes =
                    ProjectExternalModIntegrationOutcomesForSelectedContributions(
                        capabilitySelection.ExternalModIntegrationOutcomes,
                        selectedContributions);
            return CreateCompletePlan(
                selectedContributions,
                externalModIntegrationOutcomes,
                statusCompatibilityDiagnostic);
        }

        internal SupportRuntimeSnapshot CreateSupportReportSnapshot(
            string installationState)
        {
            var selectedPatchGroups = new List<string>(
                OrderedPatchGroupIds.Count);
            for (int index = 0; index < OrderedPatchGroupIds.Count; index++)
            {
                selectedPatchGroups.Add(OrderedPatchGroupIds[index].Value);
            }

            var externalModIntegrations =
                new List<SupportExternalModIntegrationSnapshot>(
                    ExternalModIntegrationOutcomes.Count);
            for (int outcomeIndex = 0;
                 outcomeIndex < ExternalModIntegrationOutcomes.Count;
                 outcomeIndex++)
            {
                externalModIntegrations.Add(
                    CreateExternalModIntegrationSnapshot(
                        ExternalModIntegrationOutcomes[outcomeIndex]));
            }

            return SupportRuntimeSnapshot.Available(
                installationState,
                selectedPatchGroups,
                StatusCompatibilityDiagnostic,
                externalModIntegrations);
        }

        /// <summary>
        /// Revalidates only the exact authorities captured by the immutable
        /// selected contributions. The installer invokes this at cold startup and
        /// again at the game-load boundary, never from gameplay hot paths.
        /// </summary>
        internal void VerifySelectedAuthority(
            IReadOnlyList<ActiveHarmonyPrefixDescriptor> activePrefixes)
        {
            ValidateActivePrefixes(activePrefixes);
            for (int contributionIndex = 0;
                 contributionIndex < SelectedContributions.Count;
                 contributionIndex++)
            {
                PreparedRuntimeAuthorityContribution contribution =
                    SelectedContributions[contributionIndex];
                string contributionDisplayName =
                    GetContributionDisplayName(contribution);
                for (int requirementIndex = 0;
                     requirementIndex <
                        contribution.AuthorityRequirements.Count;
                     requirementIndex++)
                {
                    VerifyAuthorityRequirement(
                        contributionDisplayName,
                        contribution.AuthorityRequirements[requirementIndex],
                        activePrefixes);
                }
            }
        }

        private static DeliveryTemperatureRuntimePatchPlan CreateCompletePlan(
            IReadOnlyList<PreparedRuntimeAuthorityContribution>
                selectedContributions,
            IReadOnlyList<ExternalModIntegrationOutcome>
                externalModIntegrationOutcomes,
            string? statusCompatibilityDiagnostic)
        {
            var copiedContributions =
                new List<PreparedRuntimeAuthorityContribution>(
                    selectedContributions.Count);
            var orderedPatchGroupIds = new List<RuntimePatchGroupId>();
            var seenPatchGroupIds = new HashSet<RuntimePatchGroupId>();
            var orderedPatchBindings =
                new List<HarmonyPatchContractBinding>();
            var seenPatchBindings = new HashSet<(
                MethodBase TargetMethod,
                MethodInfo PatchMethod,
                HarmonyPatchContractKind PatchKind)>();
            var authorityRequirements =
                new List<RuntimeAuthorityRequirement>();

            for (int contributionIndex = 0;
                 contributionIndex < selectedContributions.Count;
                 contributionIndex++)
            {
                PreparedRuntimeAuthorityContribution contribution =
                    selectedContributions[contributionIndex] ??
                    throw new ArgumentException(
                        "A selected runtime contribution cannot be null.",
                        nameof(selectedContributions));
                copiedContributions.Add(contribution);

                for (int groupIndex = 0;
                     groupIndex < contribution.PatchGroupIds.Count;
                     groupIndex++)
                {
                    RuntimePatchGroupId groupId =
                        contribution.PatchGroupIds[groupIndex];
                    if (!seenPatchGroupIds.Add(groupId))
                    {
                        throw new InvalidOperationException(
                            "A complete runtime plan cannot repeat patch-group " +
                            "identity " + groupId.Value + ".");
                    }

                    orderedPatchGroupIds.Add(groupId);
                }

                for (int bindingIndex = 0;
                     bindingIndex < contribution.PatchBindings.Count;
                     bindingIndex++)
                {
                    HarmonyPatchContractBinding binding =
                        contribution.PatchBindings[bindingIndex];
                    var bindingIdentity = (
                        binding.TargetMethod,
                        binding.PatchMethod,
                        binding.PatchKind);
                    if (!seenPatchBindings.Add(bindingIdentity))
                    {
                        throw new InvalidOperationException(
                            "A complete runtime plan cannot repeat an exact " +
                            "Harmony patch binding.");
                    }

                    orderedPatchBindings.Add(binding);
                }

                for (int requirementIndex = 0;
                     requirementIndex <
                        contribution.AuthorityRequirements.Count;
                     requirementIndex++)
                {
                    authorityRequirements.Add(
                        contribution.AuthorityRequirements[requirementIndex]);
                }
            }

            var copiedOutcomes = new List<ExternalModIntegrationOutcome>(
                externalModIntegrationOutcomes.Count);
            for (int outcomeIndex = 0;
                 outcomeIndex < externalModIntegrationOutcomes.Count;
                 outcomeIndex++)
            {
                copiedOutcomes.Add(
                    externalModIntegrationOutcomes[outcomeIndex] ??
                    throw new ArgumentException(
                        "An external-mod integration outcome cannot be null.",
                        nameof(externalModIntegrationOutcomes)));
            }

            return new DeliveryTemperatureRuntimePatchPlan(
                new ReadOnlyCollection<PreparedRuntimeAuthorityContribution>(
                    copiedContributions),
                new ReadOnlyCollection<RuntimePatchGroupId>(
                    orderedPatchGroupIds),
                HarmonyPatchContractBindingVerifier.VerifyAll(
                    orderedPatchBindings),
                new ReadOnlyCollection<RuntimeAuthorityRequirement>(
                    authorityRequirements),
                new ReadOnlyCollection<ExternalModIntegrationOutcome>(
                    copiedOutcomes),
                statusCompatibilityDiagnostic);
        }

        private static bool IsStatusResponsibility(
            RuntimeCapabilityId capabilityId) =>
            capabilityId.Equals(
                RuntimeCapabilityId.WorldInventoryTemperaturePublication) ||
            capabilityId.Equals(
                RuntimeCapabilityId.TemperatureStatusAvailability);

        private static string CreateStatusCompatibilityDiagnostic(
            RuntimeCapabilitySelectionEntry unavailableSelection)
        {
            string diagnosticCode = unavailableSelection.DiagnosticCode ??
                throw new InvalidOperationException(
                    "An unavailable optional status responsibility requires a " +
                    "stable diagnostic code.");
            return "Temperature-aware resource-status integration is disabled " +
                "for this loaded game; existing ONI status availability remains " +
                "unchanged. Runtime capability " +
                unavailableSelection.CapabilityId.Value +
                " is unavailable (" + diagnosticCode + ").";
        }

        private static void ValidateActivePrefixes(
            IReadOnlyList<ActiveHarmonyPrefixDescriptor> activePrefixes)
        {
            if (activePrefixes == null)
            {
                throw new ArgumentNullException(nameof(activePrefixes));
            }

            for (int prefixIndex = 0;
                 prefixIndex < activePrefixes.Count;
                 prefixIndex++)
            {
                if (activePrefixes[prefixIndex] == null)
                {
                    throw new ArgumentException(
                        "An active Harmony prefix descriptor cannot be null.",
                        nameof(activePrefixes));
                }
            }
        }

        private static void VerifyAuthorityRequirement(
            string contributionDisplayName,
            RuntimeAuthorityRequirement requirement,
            IReadOnlyList<ActiveHarmonyPrefixDescriptor> activePrefixes)
        {
            switch (requirement.Kind)
            {
                case RuntimeAuthorityRequirementKind.KleiOriginal:
                    VerifyKleiOriginalAuthority(
                        contributionDisplayName,
                        requirement,
                        activePrefixes);
                    return;
                case RuntimeAuthorityRequirementKind.ExactOwnedReplacement:
                    VerifyExactOwnedReplacementAuthority(
                        contributionDisplayName,
                        requirement,
                        activePrefixes);
                    return;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(requirement.Kind),
                        requirement.Kind,
                        "Unknown runtime-authority requirement kind.");
            }
        }

        private static void VerifyKleiOriginalAuthority(
            string contributionDisplayName,
            RuntimeAuthorityRequirement requirement,
            IReadOnlyList<ActiveHarmonyPrefixDescriptor> activePrefixes)
        {
            if (HarmonyPatchContractVerifier.VerifyKleiAuthority(
                    requirement.TargetMethod,
                    activePrefixes,
                    requirement.PermittedSkippingPrefixOwners))
            {
                return;
            }

            ActiveHarmonyPrefixDescriptor conflictingPrefix =
                RequireConflictingSkippingPrefix(
                    requirement,
                    activePrefixes);
            throw ChangedAuthority(
                contributionDisplayName,
                requirement.TargetMethod,
                conflictingPrefix,
                "Klei's original method is no longer the proved authority");
        }

        private static void VerifyExactOwnedReplacementAuthority(
            string contributionDisplayName,
            RuntimeAuthorityRequirement requirement,
            IReadOnlyList<ActiveHarmonyPrefixDescriptor> activePrefixes)
        {
            MethodInfo requiredPrefixMethod =
                requirement.RequiredPrefixMethod ??
                throw new InvalidOperationException(
                    "An exact owned replacement requirement has no prefix method.");
            string requiredHarmonyOwner =
                requirement.RequiredHarmonyOwner ??
                throw new InvalidOperationException(
                    "An exact owned replacement requirement has no Harmony owner.");
            bool foundExactReplacement = false;
            for (int prefixIndex = 0;
                 prefixIndex < activePrefixes.Count;
                 prefixIndex++)
            {
                ActiveHarmonyPrefixDescriptor activePrefix =
                    activePrefixes[prefixIndex];
                if (Equals(
                        activePrefix.TargetMethod,
                        requirement.TargetMethod) &&
                    Equals(
                        activePrefix.PrefixMethod,
                        requiredPrefixMethod) &&
                    string.Equals(
                        activePrefix.HarmonyOwner,
                        requiredHarmonyOwner,
                        StringComparison.Ordinal))
                {
                    foundExactReplacement = true;
                    break;
                }
            }

            if (!foundExactReplacement)
            {
                throw new HarmonyPatchContractViolationException(
                    "Selected runtime contribution '" +
                    contributionDisplayName +
                    "' no longer has exact replacement prefix '" +
                    GetMethodDisplayName(requiredPrefixMethod) +
                    "' for target '" +
                    GetMethodDisplayName(requirement.TargetMethod) +
                    "' under Harmony owner '" +
                    requiredHarmonyOwner +
                    "'. No fallback was selected.");
            }

            if (HarmonyPatchContractVerifier.VerifyKleiAuthority(
                    requirement.TargetMethod,
                    activePrefixes,
                    requirement.PermittedSkippingPrefixOwners))
            {
                return;
            }

            ActiveHarmonyPrefixDescriptor conflictingPrefix =
                RequireConflictingSkippingPrefix(
                    requirement,
                    activePrefixes);
            throw ChangedAuthority(
                contributionDisplayName,
                requirement.TargetMethod,
                conflictingPrefix,
                "an unpermitted skipping prefix can supersede the selected " +
                "exact replacement");
        }

        private static ActiveHarmonyPrefixDescriptor
            RequireConflictingSkippingPrefix(
                RuntimeAuthorityRequirement requirement,
                IReadOnlyList<ActiveHarmonyPrefixDescriptor> activePrefixes)
        {
            for (int prefixIndex = 0;
                 prefixIndex < activePrefixes.Count;
                 prefixIndex++)
            {
                ActiveHarmonyPrefixDescriptor activePrefix =
                    activePrefixes[prefixIndex];
                if (Equals(
                        activePrefix.TargetMethod,
                        requirement.TargetMethod) &&
                    activePrefix.PrefixMethod.ReturnType == typeof(bool) &&
                    !ContainsExactOwner(
                        requirement.PermittedSkippingPrefixOwners,
                        activePrefix.HarmonyOwner))
                {
                    return activePrefix;
                }
            }

            throw new InvalidOperationException(
                "Runtime authority verification reported a conflict without an " +
                "identifiable unpermitted skipping prefix.");
        }

        private static HarmonyPatchContractViolationException ChangedAuthority(
            string contributionDisplayName,
            MethodBase targetMethod,
            ActiveHarmonyPrefixDescriptor conflictingPrefix,
            string reason) =>
            new HarmonyPatchContractViolationException(
                "Selected runtime contribution '" +
                contributionDisplayName +
                "' failed its authority check for target '" +
                GetMethodDisplayName(targetMethod) +
                "': " + reason +
                ". Conflicting prefix '" +
                GetMethodDisplayName(conflictingPrefix.PrefixMethod) +
                "', Harmony owner '" +
                conflictingPrefix.HarmonyOwner +
                "', priority " +
                conflictingPrefix.Priority + ".");

        private static string GetContributionDisplayName(
            PreparedRuntimeAuthorityContribution contribution)
        {
            var patchGroupIds = new string[contribution.PatchGroupIds.Count];
            for (int index = 0;
                 index < contribution.PatchGroupIds.Count;
                 index++)
            {
                patchGroupIds[index] = contribution.PatchGroupIds[index].Value;
            }

            return patchGroupIds.Length == 0
                ? contribution.CapabilityId.Value
                : string.Join(", ", patchGroupIds);
        }

        private static bool ContainsExactOwner(
            IReadOnlyList<string> permittedOwners,
            string candidateOwner)
        {
            for (int ownerIndex = 0;
                 ownerIndex < permittedOwners.Count;
                 ownerIndex++)
            {
                if (string.Equals(
                        permittedOwners[ownerIndex],
                        candidateOwner,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetMethodDisplayName(MethodBase method) =>
            (method.DeclaringType?.FullName ?? "<unknown-type>") +
            "." + method.Name;

        private static IReadOnlyList<ExternalModIntegrationOutcome>
            ProjectExternalModIntegrationOutcomesForSelectedContributions(
                IReadOnlyList<ExternalModIntegrationOutcome> outcomes,
                IReadOnlyList<PreparedRuntimeAuthorityContribution>
                    selectedContributions)
        {
            var selectedExternalCapabilities = new HashSet<(
                DeclaredModIntegrationId IntegrationId,
                RuntimeCapabilityId CapabilityId)>();
            for (int contributionIndex = 0;
                 contributionIndex < selectedContributions.Count;
                 contributionIndex++)
            {
                PreparedRuntimeAuthorityContribution contribution =
                    selectedContributions[contributionIndex];
                DeclaredModIntegrationId? integrationId = contribution
                    .ImplementationIdentity.DeclaredExternalIntegrationId;
                if (integrationId.HasValue)
                {
                    selectedExternalCapabilities.Add((
                        integrationId.Value,
                        contribution.CapabilityId));
                }
            }

            var projectedOutcomes = new List<ExternalModIntegrationOutcome>(
                outcomes.Count);
            for (int outcomeIndex = 0;
                 outcomeIndex < outcomes.Count;
                 outcomeIndex++)
            {
                ExternalModIntegrationOutcome outcome = outcomes[outcomeIndex];
                var projectedCapabilities = new List<
                    ExternalModIntegrationCapabilityOutcome>(
                        outcome.Capabilities.Count);
                bool outcomeChanged = false;
                for (int capabilityIndex = 0;
                     capabilityIndex < outcome.Capabilities.Count;
                     capabilityIndex++)
                {
                    ExternalModIntegrationCapabilityOutcome capability =
                        outcome.Capabilities[capabilityIndex];
                    IntegrationCapabilityDisposition disposition =
                        capability.Disposition ==
                                IntegrationCapabilityDisposition.Selected &&
                            !selectedExternalCapabilities.Contains((
                                outcome.IntegrationId,
                                capability.CapabilityId))
                            ? IntegrationCapabilityDisposition.Ready
                            : capability.Disposition;
                    outcomeChanged |= disposition != capability.Disposition;
                    projectedCapabilities.Add(
                        new ExternalModIntegrationCapabilityOutcome(
                            capability.CapabilityId,
                            capability.Category,
                            capability.AuthorityObservation,
                            capability.ContractState,
                            disposition,
                            capability.DiagnosticCode,
                            capability.DiagnosticMessage));
                }

                projectedOutcomes.Add(outcomeChanged
                    ? new ExternalModIntegrationOutcome(
                        outcome.IntegrationId,
                        outcome.DisplayName,
                        outcome.Categories,
                        outcome.MatchState,
                        outcome.AssemblyIdentity,
                        outcome.AssemblyVersion,
                        outcome.FileVersion,
                        outcome.AssemblySha256,
                        projectedCapabilities,
                        outcome.Diagnostics)
                    : outcome);
            }

            return new ReadOnlyCollection<ExternalModIntegrationOutcome>(
                projectedOutcomes);
        }

        private static SupportExternalModIntegrationSnapshot
            CreateExternalModIntegrationSnapshot(
                ExternalModIntegrationOutcome outcome)
        {
            var categories = new List<string>(outcome.Categories.Count);
            for (int categoryIndex = 0;
                 categoryIndex < outcome.Categories.Count;
                 categoryIndex++)
            {
                categories.Add(GetSupportCategoryName(
                    outcome.Categories[categoryIndex]));
            }

            var capabilities =
                new List<SupportExternalModCapabilitySnapshot>(
                    outcome.Capabilities.Count);
            for (int capabilityIndex = 0;
                 capabilityIndex < outcome.Capabilities.Count;
                 capabilityIndex++)
            {
                ExternalModIntegrationCapabilityOutcome capability =
                    outcome.Capabilities[capabilityIndex];
                capabilities.Add(
                    new SupportExternalModCapabilitySnapshot(
                        capability.CapabilityId.Value,
                        GetSupportAuthorityObservationName(
                            capability.AuthorityObservation),
                        GetSupportContractStateName(
                            capability.ContractState),
                        GetSupportDispositionName(
                            capability.Disposition),
                        capability.DiagnosticCode,
                        capability.DiagnosticMessage));
            }

            // The generic outcome's integration-level diagnostics have no
            // occurrence timestamps. Their exact bounded code/message pairs
            // are already retained by the affected capability snapshots. The
            // timestamped schema collection remains an inert extension slot
            // until an integration publishes genuine support diagnostics.
            return new SupportExternalModIntegrationSnapshot(
                outcome.IntegrationId.Value,
                outcome.DisplayName,
                categories,
                GetSupportMatchStateName(outcome.MatchState),
                CreateOptionalSupportFact(
                    outcome.AssemblyIdentity,
                    "Declared integration assembly identity was not observed."),
                CreateOptionalSupportFact(
                    outcome.AssemblyVersion,
                    "Declared integration assembly version was not observed."),
                CreateOptionalSupportFact(
                    outcome.FileVersion,
                    "Declared integration file version was not observed."),
                CreateOptionalSupportFact(
                    outcome.AssemblySha256,
                    "Declared integration assembly SHA-256 was not observed."),
                capabilities,
                Array.Empty<SupportDiagnosticSnapshot>());
        }

        private static string GetSupportCategoryName(
            ExternalModIntegrationCategory category)
        {
            switch (category)
            {
                case ExternalModIntegrationCategory.ExclusiveRuntimeAuthority:
                    return "exclusive-runtime-authority";
                case ExternalModIntegrationCategory.AdditiveInteroperability:
                    return "additive-interoperability";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(category),
                        category,
                        "Unknown external-mod integration category.");
            }
        }

        private static string GetSupportMatchStateName(
            DeclaredModMatchState matchState)
        {
            switch (matchState)
            {
                case DeclaredModMatchState.NotMatched:
                    return "not-matched";
                case DeclaredModMatchState.Matched:
                    return "matched";
                case DeclaredModMatchState.Ambiguous:
                    return "ambiguous";
                case DeclaredModMatchState.InspectionUnavailable:
                    return "inspection-unavailable";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(matchState),
                        matchState,
                        "Unknown declared-mod match state.");
            }
        }

        private static string GetSupportAuthorityObservationName(
            RuntimeAuthorityObservation authorityObservation)
        {
            switch (authorityObservation)
            {
                case RuntimeAuthorityObservation.DoesNotOwn:
                    return "does-not-own";
                case RuntimeAuthorityObservation.OwnsCompatible:
                    return "owns-compatible";
                case RuntimeAuthorityObservation.OwnsIncompatible:
                    return "owns-incompatible";
                case RuntimeAuthorityObservation.OwnershipUnavailable:
                    return "ownership-unavailable";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(authorityObservation),
                        authorityObservation,
                        "Unknown runtime-authority observation.");
            }
        }

        private static string GetSupportContractStateName(
            IntegrationContractState contractState)
        {
            switch (contractState)
            {
                case IntegrationContractState.NotEvaluated:
                    return "not-evaluated";
                case IntegrationContractState.Compatible:
                    return "compatible";
                case IntegrationContractState.Incompatible:
                    return "incompatible";
                case IntegrationContractState.VerificationUnavailable:
                    return "verification-unavailable";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(contractState),
                        contractState,
                        "Unknown integration contract state.");
            }
        }

        private static string GetSupportDispositionName(
            IntegrationCapabilityDisposition disposition)
        {
            switch (disposition)
            {
                case IntegrationCapabilityDisposition.NotApplicable:
                    return "not-applicable";
                case IntegrationCapabilityDisposition.Selected:
                    return "selected";
                case IntegrationCapabilityDisposition.Ready:
                    return "ready";
                case IntegrationCapabilityDisposition.Unavailable:
                    return "unavailable";
                case IntegrationCapabilityDisposition.ActivationBlocking:
                    return "activation-blocking";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(disposition),
                        disposition,
                        "Unknown integration capability disposition.");
            }
        }

        private static SupportReportFact CreateOptionalSupportFact(
            object? value,
            string unavailableReason) =>
            value == null
                ? SupportReportFact.Unavailable(unavailableReason)
                : SupportReportFact.Available(
                    value.ToString() ??
                    throw new InvalidOperationException(
                        "An observed integration fact could not be formatted."));
    }
}
