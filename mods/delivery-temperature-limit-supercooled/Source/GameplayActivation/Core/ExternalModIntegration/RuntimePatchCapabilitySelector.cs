#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Applies Temperature Limit-owned criticality and coherence policy to
    /// complete provider-neutral runtime-authority observations.
    /// </summary>
    internal static class RuntimePatchCapabilitySelector
    {
        internal static RuntimePatchCapabilitySelection Select(
            IReadOnlyList<RuntimeCapabilityDefinition> definitions,
            IReadOnlyList<PreparedRuntimeAuthorityContribution> contributions,
            IReadOnlyList<ExternalModIntegrationOutcome> outcomes)
        {
            SelectionInputs inputs = ValidateInputs(
                definitions,
                contributions,
                outcomes);
            var selected = new List<RuntimeCapabilitySelectionEntry>(
                definitions.Count);
            var outcomeProjections = new Dictionary<
                (DeclaredModIntegrationId IntegrationId,
                 RuntimeCapabilityId CapabilityId),
                CapabilityOutcomeProjection>();

            for (int definitionIndex = 0;
                 definitionIndex < definitions.Count;
                 definitionIndex++)
            {
                RuntimeCapabilityDefinition definition =
                    definitions[definitionIndex];
                IReadOnlyList<PreparedRuntimeAuthorityContribution> claims =
                    FindClaims(definition.Id, contributions);

                if (claims.Count == 0)
                {
                    SelectBaselineOrOmit(
                        definition,
                        selected,
                        ProjectFinalOutcomes(
                            inputs.Outcomes,
                            outcomeProjections));
                    continue;
                }

                if (claims.Count != 1)
                {
                    throw RuntimeCapabilitySelectionException.ConflictingOwners(
                        definition.Id,
                        claims,
                        ProjectFinalOutcomes(
                            inputs.Outcomes,
                            outcomeProjections));
                }

                SelectSingleClaim(
                    definition,
                    claims[0],
                    selected,
                    outcomeProjections,
                    inputs.Outcomes);
            }

            ValidateAtomicBundles(
                selected,
                inputs.Outcomes,
                outcomeProjections);
            IReadOnlyList<ExternalModIntegrationOutcome> finalOutcomes =
                ProjectFinalOutcomes(inputs.Outcomes, outcomeProjections);
            return new RuntimePatchCapabilitySelection(
                selected,
                finalOutcomes);
        }

        private static SelectionInputs ValidateInputs(
            IReadOnlyList<RuntimeCapabilityDefinition> definitions,
            IReadOnlyList<PreparedRuntimeAuthorityContribution> contributions,
            IReadOnlyList<ExternalModIntegrationOutcome> outcomes)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            if (contributions == null)
            {
                throw new ArgumentNullException(nameof(contributions));
            }

            if (outcomes == null)
            {
                throw new ArgumentNullException(nameof(outcomes));
            }

            var definitionIds = new HashSet<RuntimeCapabilityId>();
            for (int index = 0; index < definitions.Count; index++)
            {
                RuntimeCapabilityDefinition definition = definitions[index];
                if (definition == null)
                {
                    throw new ArgumentException(
                        "A runtime capability definition cannot be null.",
                        nameof(definitions));
                }

                if (!definitionIds.Add(definition.Id))
                {
                    throw new ArgumentException(
                        "A runtime capability cannot be defined twice.",
                        nameof(definitions));
                }
            }

            var outcomesById = new Dictionary<
                DeclaredModIntegrationId,
                ExternalModIntegrationOutcome>();
            var capabilityOutcomesByKey = new Dictionary<
                (DeclaredModIntegrationId IntegrationId,
                 RuntimeCapabilityId CapabilityId),
                ExternalModIntegrationCapabilityOutcome>();
            for (int index = 0; index < outcomes.Count; index++)
            {
                ExternalModIntegrationOutcome outcome = outcomes[index];
                if (outcome == null)
                {
                    throw new ArgumentException(
                        "An external-mod integration outcome cannot be null.",
                        nameof(outcomes));
                }

                if (outcomesById.ContainsKey(outcome.IntegrationId))
                {
                    throw new ArgumentException(
                        "An integration outcome cannot be repeated.",
                        nameof(outcomes));
                }

                outcomesById.Add(outcome.IntegrationId, outcome);
                for (int capabilityIndex = 0;
                     capabilityIndex < outcome.Capabilities.Count;
                     capabilityIndex++)
                {
                    ExternalModIntegrationCapabilityOutcome capabilityOutcome =
                        outcome.Capabilities[capabilityIndex];
                    RuntimeCapabilityId capabilityId =
                        capabilityOutcome.CapabilityId;
                    capabilityOutcomesByKey.Add(
                        (outcome.IntegrationId, capabilityId),
                        capabilityOutcome);
                    if (capabilityOutcome.Category ==
                            ExternalModIntegrationCategory
                                .ExclusiveRuntimeAuthority &&
                        !definitionIds.Contains(capabilityId))
                    {
                        throw new RuntimeCapabilitySelectionException(
                            capabilityId,
                            "undeclared-runtime-capability-outcome",
                            "An external integration returned an outcome for a " +
                            "runtime capability Temperature Limit did not define.",
                            outcomes);
                    }
                }
            }

            var contributionKeys = new HashSet<(
                DeclaredModIntegrationId IntegrationId,
                RuntimeCapabilityId CapabilityId)>();
            for (int index = 0; index < contributions.Count; index++)
            {
                PreparedRuntimeAuthorityContribution contribution =
                    contributions[index];
                if (contribution == null)
                {
                    throw new ArgumentException(
                        "A prepared runtime-authority contribution cannot be null.",
                        nameof(contributions));
                }

                if (!definitionIds.Contains(contribution.CapabilityId))
                {
                    throw new RuntimeCapabilitySelectionException(
                        contribution.CapabilityId,
                        "undeclared-runtime-capability-contribution",
                        "An external integration contributed a runtime capability " +
                        "Temperature Limit did not define.",
                        outcomes);
                }

                DeclaredModIntegrationId? declaredExternalIntegrationId =
                    contribution.ImplementationIdentity
                        .DeclaredExternalIntegrationId;
                if (!declaredExternalIntegrationId.HasValue)
                {
                    throw new RuntimeCapabilitySelectionException(
                        contribution.CapabilityId,
                        "non-external-runtime-authority-contribution",
                        "The selector's external contribution input cannot " +
                        "contain a built-in Klei baseline contribution.",
                        outcomes);
                }

                var key = (
                    declaredExternalIntegrationId.Value,
                    contribution.CapabilityId);
                ExternalModIntegrationCapabilityOutcome? capabilityOutcome;
                if (!capabilityOutcomesByKey.TryGetValue(
                        key,
                        out capabilityOutcome))
                {
                    throw new RuntimeCapabilitySelectionException(
                        contribution.CapabilityId,
                        "unreported-runtime-authority-contribution",
                        "Every prepared external runtime-authority contribution " +
                        "requires the matching declared integration outcome.",
                        outcomes);
                }

                if (capabilityOutcome.Category !=
                    ExternalModIntegrationCategory.ExclusiveRuntimeAuthority)
                {
                    throw new RuntimeCapabilitySelectionException(
                        contribution.CapabilityId,
                        "additive-runtime-authority-contribution",
                        "An additive interoperability capability cannot supply " +
                        "a runtime-authority contribution.",
                        outcomes);
                }

                if (capabilityOutcome.AuthorityObservation !=
                    contribution.AuthorityObservation)
                {
                    throw new RuntimeCapabilitySelectionException(
                        contribution.CapabilityId,
                        "contradictory-runtime-authority-observation",
                        "A prepared runtime-authority contribution must exactly " +
                        "match its sanitized capability authority observation.",
                        outcomes);
                }

                IntegrationContractState requiredContractState =
                    GetRequiredPreparedContractState(
                        contribution.AuthorityObservation);
                if (capabilityOutcome.ContractState != requiredContractState)
                {
                    throw new RuntimeCapabilitySelectionException(
                        contribution.CapabilityId,
                        "contradictory-runtime-authority-contract-state",
                        "A prepared external runtime-authority contribution " +
                        "requires the matching sanitized contract state.",
                        outcomes);
                }

                IntegrationCapabilityDisposition requiredDisposition =
                    GetRequiredPreparedDisposition(
                        contribution.AuthorityObservation);
                if (capabilityOutcome.Disposition != requiredDisposition)
                {
                    throw new RuntimeCapabilitySelectionException(
                        contribution.CapabilityId,
                        "contradictory-runtime-authority-disposition",
                        "A prepared external runtime-authority contribution " +
                        "requires the matching pre-selection disposition.",
                        outcomes);
                }

                if (!contributionKeys.Add(key))
                {
                    throw new RuntimeCapabilitySelectionException(
                        contribution.CapabilityId,
                        "duplicate-runtime-authority-contribution",
                        "One integration cannot contribute the same runtime " +
                        "capability twice.",
                        outcomes);
                }
            }

            foreach (KeyValuePair<
                         (DeclaredModIntegrationId IntegrationId,
                          RuntimeCapabilityId CapabilityId),
                         ExternalModIntegrationCapabilityOutcome> entry in
                     capabilityOutcomesByKey)
            {
                if (entry.Value.Category ==
                        ExternalModIntegrationCategory
                            .ExclusiveRuntimeAuthority &&
                    entry.Value.AuthorityObservation !=
                        RuntimeAuthorityObservation.DoesNotOwn &&
                    !contributionKeys.Contains(entry.Key))
                {
                    throw new RuntimeCapabilitySelectionException(
                        entry.Key.CapabilityId,
                        "missing-runtime-authority-contribution",
                        "Every owning or unavailable external runtime-authority " +
                        "observation requires a matching prepared contribution.",
                        outcomes);
                }
            }

            return new SelectionInputs(outcomes);
        }

        private static IReadOnlyList<PreparedRuntimeAuthorityContribution>
            FindClaims(
                RuntimeCapabilityId capabilityId,
                IReadOnlyList<PreparedRuntimeAuthorityContribution>
                    contributions)
        {
            var claims = new List<PreparedRuntimeAuthorityContribution>();
            for (int index = 0; index < contributions.Count; index++)
            {
                PreparedRuntimeAuthorityContribution contribution =
                    contributions[index];
                if (contribution.CapabilityId.Equals(capabilityId) &&
                    contribution.AuthorityObservation !=
                        RuntimeAuthorityObservation.DoesNotOwn)
                {
                    claims.Add(contribution);
                }
            }

            return new ReadOnlyCollection<
                PreparedRuntimeAuthorityContribution>(claims);
        }

        private static void SelectBaselineOrOmit(
            RuntimeCapabilityDefinition definition,
            ICollection<RuntimeCapabilitySelectionEntry> selected,
            IReadOnlyList<ExternalModIntegrationOutcome> outcomes)
        {
            if (definition.KleiBaselineContribution != null)
            {
                selected.Add(
                    RuntimeCapabilitySelectionEntry.ForSelectedContribution(
                        definition,
                        definition.KleiBaselineContribution));
                return;
            }

            if (definition.IsRequired)
            {
                throw new RuntimeCapabilitySelectionException(
                    definition.Id,
                    "required-runtime-capability-without-implementation",
                    "No Klei baseline or compatible declared external owner can " +
                    "implement required runtime capability " +
                    definition.Id.Value +
                    ".",
                    outcomes);
            }

            selected.Add(
                RuntimeCapabilitySelectionEntry.ForOptionalOmission(
                    definition,
                    "optional-runtime-capability-without-implementation",
                    "No Klei baseline or compatible declared external owner " +
                    "implements optional runtime capability " +
                    definition.Id.Value +
                    "."));
        }

        private static void SelectSingleClaim(
            RuntimeCapabilityDefinition definition,
            PreparedRuntimeAuthorityContribution claim,
            ICollection<RuntimeCapabilitySelectionEntry> selected,
            Dictionary<
                (DeclaredModIntegrationId IntegrationId,
                 RuntimeCapabilityId CapabilityId),
                CapabilityOutcomeProjection> outcomeProjections,
            IReadOnlyList<ExternalModIntegrationOutcome> outcomes)
        {
            DeclaredModIntegrationId integrationId =
                claim.ImplementationIdentity.DeclaredExternalIntegrationId ??
                throw new InvalidOperationException(
                    "A selected external claim must retain its declared " +
                    "integration identity.");
            var outcomeKey = (integrationId, claim.CapabilityId);
            if (claim.AuthorityObservation ==
                RuntimeAuthorityObservation.OwnsCompatible)
            {
                selected.Add(
                    RuntimeCapabilitySelectionEntry.ForSelectedContribution(
                        definition,
                        claim));
                outcomeProjections[outcomeKey] =
                    new CapabilityOutcomeProjection(
                        IntegrationCapabilityDisposition.Selected);
                return;
            }

            if (definition.IsRequired)
            {
                outcomeProjections[outcomeKey] =
                    new CapabilityOutcomeProjection(
                        IntegrationCapabilityDisposition.ActivationBlocking);
                throw new RuntimeCapabilitySelectionException(
                    definition.Id,
                    "required-runtime-capability-unavailable",
                    "The declared external owner of required runtime capability " +
                    definition.Id.Value +
                    " did not supply a compatible complete contribution.",
                    ProjectFinalOutcomes(outcomes, outcomeProjections));
            }

            selected.Add(
                RuntimeCapabilitySelectionEntry.ForOptionalOmission(
                    definition,
                    claim.DiagnosticCode ??
                        throw new InvalidOperationException(
                            "An unavailable optional runtime-authority " +
                            "contribution requires a diagnostic code."),
                    claim.DiagnosticMessage ??
                        throw new InvalidOperationException(
                            "An unavailable optional runtime-authority " +
                            "contribution requires a diagnostic message.")));
            outcomeProjections[outcomeKey] =
                new CapabilityOutcomeProjection(
                    IntegrationCapabilityDisposition.Unavailable);
        }

        private static void ValidateAtomicBundles(
            IReadOnlyList<RuntimeCapabilitySelectionEntry> selections,
            IReadOnlyList<ExternalModIntegrationOutcome> outcomes,
            Dictionary<
                (DeclaredModIntegrationId IntegrationId,
                 RuntimeCapabilityId CapabilityId),
                CapabilityOutcomeProjection> outcomeProjections)
        {
            var selectedOwnerByBundle = new Dictionary<
                RuntimeCapabilityBundleId,
                RuntimeAuthorityImplementationIdentity?>();
            var firstCapabilityByBundle = new Dictionary<
                RuntimeCapabilityBundleId,
                RuntimeCapabilityId>();

            for (int index = 0; index < selections.Count; index++)
            {
                RuntimeCapabilitySelectionEntry selection = selections[index];
                if (!selection.Definition.AtomicBundleId.HasValue)
                {
                    continue;
                }

                RuntimeCapabilityBundleId bundleId =
                    selection.Definition.AtomicBundleId.Value;
                RuntimeAuthorityImplementationIdentity? selectedOwner =
                    selection.SelectedContribution == null
                        ? (RuntimeAuthorityImplementationIdentity?)null
                        : selection.SelectedContribution
                            .ImplementationIdentity;

                RuntimeAuthorityImplementationIdentity? existingOwner;
                if (!selectedOwnerByBundle.TryGetValue(
                        bundleId,
                        out existingOwner))
                {
                    selectedOwnerByBundle.Add(bundleId, selectedOwner);
                    firstCapabilityByBundle.Add(
                        bundleId,
                        selection.CapabilityId);
                    continue;
                }

                if (!Nullable.Equals(existingOwner, selectedOwner))
                {
                    MarkExternallyReportedBundleCapabilitiesActivationBlocking(
                        bundleId,
                        selections,
                        outcomes,
                        outcomeProjections);
                    throw new RuntimeCapabilitySelectionException(
                        firstCapabilityByBundle[bundleId],
                        "mixed-runtime-capability-bundle",
                        "Atomic runtime capability bundle " +
                        bundleId.Value +
                        " cannot combine different implementation owners or " +
                        "mix selected and omitted members.",
                        ProjectFinalOutcomes(outcomes, outcomeProjections));
                }
            }
        }

        private static void
            MarkExternallyReportedBundleCapabilitiesActivationBlocking(
                RuntimeCapabilityBundleId bundleId,
                IReadOnlyList<RuntimeCapabilitySelectionEntry> selections,
                IReadOnlyList<ExternalModIntegrationOutcome> outcomes,
                Dictionary<
                    (DeclaredModIntegrationId IntegrationId,
                     RuntimeCapabilityId CapabilityId),
                    CapabilityOutcomeProjection> outcomeProjections)
        {
            var bundleCapabilityIds = new HashSet<RuntimeCapabilityId>();
            for (int selectionIndex = 0;
                 selectionIndex < selections.Count;
                 selectionIndex++)
            {
                RuntimeCapabilitySelectionEntry selection =
                    selections[selectionIndex];
                if (selection.Definition.AtomicBundleId.HasValue &&
                    selection.Definition.AtomicBundleId.Value.Equals(bundleId))
                {
                    bundleCapabilityIds.Add(selection.CapabilityId);
                }
            }

            for (int outcomeIndex = 0;
                 outcomeIndex < outcomes.Count;
                 outcomeIndex++)
            {
                ExternalModIntegrationOutcome outcome = outcomes[outcomeIndex];
                for (int capabilityIndex = 0;
                     capabilityIndex < outcome.Capabilities.Count;
                     capabilityIndex++)
                {
                    ExternalModIntegrationCapabilityOutcome reportedCapability =
                        outcome.Capabilities[capabilityIndex];
                    if (reportedCapability.Category !=
                            ExternalModIntegrationCategory
                                .ExclusiveRuntimeAuthority ||
                        !bundleCapabilityIds.Contains(
                            reportedCapability.CapabilityId))
                    {
                        continue;
                    }

                    outcomeProjections[(
                        outcome.IntegrationId,
                        reportedCapability.CapabilityId)] =
                        new CapabilityOutcomeProjection(
                            IntegrationCapabilityDisposition
                                .ActivationBlocking,
                            "mixed-runtime-capability-bundle",
                            "The capability belongs to an atomic runtime " +
                            "bundle whose members did not resolve to one " +
                            "implementation owner.");
                }
            }
        }

        private static IntegrationContractState
            GetRequiredPreparedContractState(
                RuntimeAuthorityObservation authorityObservation)
        {
            switch (authorityObservation)
            {
                case RuntimeAuthorityObservation.DoesNotOwn:
                    return IntegrationContractState.NotEvaluated;
                case RuntimeAuthorityObservation.OwnsCompatible:
                    return IntegrationContractState.Compatible;
                case RuntimeAuthorityObservation.OwnsIncompatible:
                    return IntegrationContractState.Incompatible;
                case RuntimeAuthorityObservation.OwnershipUnavailable:
                    return IntegrationContractState.VerificationUnavailable;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(authorityObservation),
                        authorityObservation,
                        "Unknown runtime-authority observation.");
            }
        }

        private static IntegrationCapabilityDisposition
            GetRequiredPreparedDisposition(
                RuntimeAuthorityObservation authorityObservation)
        {
            switch (authorityObservation)
            {
                case RuntimeAuthorityObservation.DoesNotOwn:
                    return IntegrationCapabilityDisposition.NotApplicable;
                case RuntimeAuthorityObservation.OwnsCompatible:
                    return IntegrationCapabilityDisposition.Ready;
                case RuntimeAuthorityObservation.OwnsIncompatible:
                case RuntimeAuthorityObservation.OwnershipUnavailable:
                    return IntegrationCapabilityDisposition.Unavailable;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(authorityObservation),
                        authorityObservation,
                        "Unknown runtime-authority observation.");
            }
        }

        private static IReadOnlyList<ExternalModIntegrationOutcome>
            ProjectFinalOutcomes(
                IReadOnlyList<ExternalModIntegrationOutcome> outcomes,
                IReadOnlyDictionary<
                    (DeclaredModIntegrationId IntegrationId,
                     RuntimeCapabilityId CapabilityId),
                    CapabilityOutcomeProjection> outcomeProjections)
        {
            var projected = new List<ExternalModIntegrationOutcome>(
                outcomes.Count);
            for (int outcomeIndex = 0;
                 outcomeIndex < outcomes.Count;
                 outcomeIndex++)
            {
                ExternalModIntegrationOutcome outcome = outcomes[outcomeIndex];
                var capabilities = new List<
                    ExternalModIntegrationCapabilityOutcome>(
                        outcome.Capabilities.Count);
                for (int capabilityIndex = 0;
                     capabilityIndex < outcome.Capabilities.Count;
                     capabilityIndex++)
                {
                    ExternalModIntegrationCapabilityOutcome capability =
                        outcome.Capabilities[capabilityIndex];
                    CapabilityOutcomeProjection projection;
                    if (!outcomeProjections.TryGetValue(
                            (outcome.IntegrationId, capability.CapabilityId),
                            out projection))
                    {
                        projection = new CapabilityOutcomeProjection(
                            capability.Disposition);
                    }

                    capabilities.Add(
                        new ExternalModIntegrationCapabilityOutcome(
                            capability.CapabilityId,
                            capability.Category,
                            capability.AuthorityObservation,
                            capability.ContractState,
                            projection.Disposition,
                            projection.DiagnosticCode ??
                                capability.DiagnosticCode,
                            projection.DiagnosticMessage ??
                                capability.DiagnosticMessage));
                }

                projected.Add(new ExternalModIntegrationOutcome(
                    outcome.IntegrationId,
                    outcome.DisplayName,
                    outcome.Categories,
                    outcome.MatchState,
                    outcome.AssemblyIdentity,
                    outcome.AssemblyVersion,
                    outcome.FileVersion,
                    outcome.AssemblySha256,
                    capabilities,
                    outcome.Diagnostics));
            }

            return new ReadOnlyCollection<ExternalModIntegrationOutcome>(
                projected);
        }

        private sealed class SelectionInputs
        {
            internal SelectionInputs(
                IReadOnlyList<ExternalModIntegrationOutcome> outcomes)
            {
                Outcomes = outcomes;
            }

            internal IReadOnlyList<ExternalModIntegrationOutcome>
                Outcomes { get; }
        }

        private readonly struct CapabilityOutcomeProjection
        {
            internal CapabilityOutcomeProjection(
                IntegrationCapabilityDisposition disposition,
                string? diagnosticCode = null,
                string? diagnosticMessage = null)
            {
                Disposition = disposition;
                DiagnosticCode = diagnosticCode;
                DiagnosticMessage = diagnosticMessage;
            }

            internal IntegrationCapabilityDisposition Disposition { get; }

            internal string? DiagnosticCode { get; }

            internal string? DiagnosticMessage { get; }
        }
    }
}
