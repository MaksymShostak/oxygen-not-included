#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Final selection result for one declared semantic capability. The
    /// factories distinguish a selected implementation from an explicitly
    /// diagnosed optional omission.
    /// </summary>
    internal sealed class RuntimeCapabilitySelectionEntry
    {
        private RuntimeCapabilitySelectionEntry(
            RuntimeCapabilityDefinition definition,
            PreparedRuntimeAuthorityContribution? selectedContribution,
            IntegrationCapabilityDisposition disposition,
            string? diagnosticCode,
            string? diagnosticMessage)
        {
            Definition = definition;
            SelectedContribution = selectedContribution;
            Disposition = disposition;
            DiagnosticCode = diagnosticCode;
            DiagnosticMessage = diagnosticMessage;
        }

        internal static RuntimeCapabilitySelectionEntry
            ForSelectedContribution(
                RuntimeCapabilityDefinition definition,
                PreparedRuntimeAuthorityContribution selectedContribution)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (selectedContribution == null)
            {
                throw new ArgumentNullException(nameof(selectedContribution));
            }

            if (!selectedContribution.CapabilityId.Equals(definition.Id))
            {
                throw new ArgumentException(
                    "A selected contribution must implement the capability " +
                    "represented by its selection entry.",
                    nameof(selectedContribution));
            }

            if (selectedContribution.AuthorityObservation !=
                RuntimeAuthorityObservation.OwnsCompatible)
            {
                throw new ArgumentException(
                    "A selected contribution must provide compatible runtime " +
                    "authority for its capability.",
                    nameof(selectedContribution));
            }

            return new RuntimeCapabilitySelectionEntry(
                definition,
                selectedContribution,
                IntegrationCapabilityDisposition.Selected,
                null,
                null);
        }

        internal static RuntimeCapabilitySelectionEntry ForOptionalOmission(
            RuntimeCapabilityDefinition definition,
            string diagnosticCode,
            string diagnosticMessage)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (definition.IsRequired)
            {
                throw new ArgumentException(
                    "A required runtime capability cannot be omitted.",
                    nameof(definition));
            }

            string validatedDiagnosticCode =
                ExternalModIntegrationModelValidation.RequireDiagnosticCode(
                    diagnosticCode,
                    nameof(diagnosticCode));
            string validatedDiagnosticMessage =
                ExternalModIntegrationModelValidation
                    .RequireDiagnosticMessage(
                        diagnosticMessage,
                        nameof(diagnosticMessage));

            return new RuntimeCapabilitySelectionEntry(
                definition,
                null,
                IntegrationCapabilityDisposition.Unavailable,
                validatedDiagnosticCode,
                validatedDiagnosticMessage);
        }

        internal RuntimeCapabilityDefinition Definition { get; }

        internal RuntimeCapabilityId CapabilityId => Definition.Id;

        internal PreparedRuntimeAuthorityContribution?
            SelectedContribution { get; }

        internal IntegrationCapabilityDisposition Disposition { get; }

        internal string? DiagnosticCode { get; }

        internal string? DiagnosticMessage { get; }
    }

    /// <summary>
    /// Immutable provider-neutral capability map consumed by runtime patch-plan
    /// composition and the matching generic integration diagnostic projection.
    /// </summary>
    internal sealed class RuntimePatchCapabilitySelection
    {
        private readonly IReadOnlyDictionary<
            RuntimeCapabilityId,
            RuntimeCapabilitySelectionEntry> selectionsByCapability;

        internal RuntimePatchCapabilitySelection(
            IEnumerable<RuntimeCapabilitySelectionEntry> capabilitySelections,
            IEnumerable<ExternalModIntegrationOutcome>
                externalModIntegrationOutcomes)
        {
            if (capabilitySelections == null)
            {
                throw new ArgumentNullException(nameof(capabilitySelections));
            }

            if (externalModIntegrationOutcomes == null)
            {
                throw new ArgumentNullException(
                    nameof(externalModIntegrationOutcomes));
            }

            var copiedSelections = new List<RuntimeCapabilitySelectionEntry>();
            var byCapability = new Dictionary<
                RuntimeCapabilityId,
                RuntimeCapabilitySelectionEntry>();
            var selectedContributions =
                new List<PreparedRuntimeAuthorityContribution>();
            foreach (RuntimeCapabilitySelectionEntry selection in
                     capabilitySelections)
            {
                if (selection == null)
                {
                    throw new ArgumentException(
                        "A capability selection cannot be null.",
                        nameof(capabilitySelections));
                }

                if (byCapability.ContainsKey(selection.CapabilityId))
                {
                    throw new ArgumentException(
                        "A runtime capability selection cannot repeat a " +
                        "capability.",
                        nameof(capabilitySelections));
                }

                copiedSelections.Add(selection);
                byCapability.Add(selection.CapabilityId, selection);
                if (selection.SelectedContribution != null)
                {
                    selectedContributions.Add(
                        selection.SelectedContribution);
                }
            }

            var copiedOutcomes = new List<ExternalModIntegrationOutcome>();
            var outcomeIds = new HashSet<DeclaredModIntegrationId>();
            foreach (ExternalModIntegrationOutcome outcome in
                     externalModIntegrationOutcomes)
            {
                if (outcome == null)
                {
                    throw new ArgumentException(
                        "An external-mod integration outcome cannot be null.",
                        nameof(externalModIntegrationOutcomes));
                }

                if (!outcomeIds.Add(outcome.IntegrationId))
                {
                    throw new ArgumentException(
                        "A runtime selection cannot repeat an external-mod " +
                        "integration outcome.",
                        nameof(externalModIntegrationOutcomes));
                }

                copiedOutcomes.Add(outcome);
            }

            CapabilitySelections = new ReadOnlyCollection<
                RuntimeCapabilitySelectionEntry>(copiedSelections);
            SelectedContributions = new ReadOnlyCollection<
                PreparedRuntimeAuthorityContribution>(selectedContributions);
            ExternalModIntegrationOutcomes = new ReadOnlyCollection<
                ExternalModIntegrationOutcome>(copiedOutcomes);
            selectionsByCapability = new ReadOnlyDictionary<
                RuntimeCapabilityId,
                RuntimeCapabilitySelectionEntry>(byCapability);
        }

        internal IReadOnlyList<RuntimeCapabilitySelectionEntry>
            CapabilitySelections { get; }

        internal IReadOnlyList<PreparedRuntimeAuthorityContribution>
            SelectedContributions { get; }

        internal IReadOnlyList<ExternalModIntegrationOutcome>
            ExternalModIntegrationOutcomes { get; }

        internal RuntimeCapabilitySelectionEntry GetCapabilitySelection(
            RuntimeCapabilityId capabilityId)
        {
            RuntimeCapabilitySelectionEntry? selection;
            if (!selectionsByCapability.TryGetValue(
                    capabilityId,
                    out selection))
            {
                throw new KeyNotFoundException(
                    "No runtime selection exists for capability " +
                    capabilityId.Value +
                    ".");
            }

            return selection;
        }
    }

    internal sealed class RuntimeCapabilitySelectionException :
        InvalidOperationException
    {
        internal RuntimeCapabilitySelectionException(
            RuntimeCapabilityId capabilityId,
            string diagnosticCode,
            string message,
            IEnumerable<ExternalModIntegrationOutcome>
                externalModIntegrationOutcomes)
            : base(message)
        {
            ExternalModIntegrationModelValidation.RequireCapabilityId(
                capabilityId,
                nameof(capabilityId));
            CapabilityId = capabilityId;
            DiagnosticCode = ExternalModIntegrationModelValidation
                .RequireDiagnosticCode(
                    diagnosticCode,
                    nameof(diagnosticCode));
            ExternalModIntegrationOutcomes = CopyOutcomes(
                externalModIntegrationOutcomes);
        }

        internal RuntimeCapabilityId CapabilityId { get; }

        internal string DiagnosticCode { get; }

        internal IReadOnlyList<ExternalModIntegrationOutcome>
            ExternalModIntegrationOutcomes { get; }

        internal static RuntimeCapabilitySelectionException ConflictingOwners(
            RuntimeCapabilityId capabilityId,
            IReadOnlyList<PreparedRuntimeAuthorityContribution> claims,
            IEnumerable<ExternalModIntegrationOutcome>
                externalModIntegrationOutcomes) =>
            new RuntimeCapabilitySelectionException(
                capabilityId,
                "conflicting-runtime-authority-owners",
                "More than one declared external integration claims exclusive " +
                "runtime authority for capability " +
                capabilityId.Value +
                "; catalog or load order cannot choose between " +
                claims.Count +
                " owners.",
                externalModIntegrationOutcomes);

        private static IReadOnlyList<ExternalModIntegrationOutcome> CopyOutcomes(
            IEnumerable<ExternalModIntegrationOutcome> outcomes)
        {
            if (outcomes == null)
            {
                throw new ArgumentNullException(nameof(outcomes));
            }

            var copied = new List<ExternalModIntegrationOutcome>();
            var integrationIds = new HashSet<DeclaredModIntegrationId>();
            foreach (ExternalModIntegrationOutcome outcome in outcomes)
            {
                if (outcome == null)
                {
                    throw new ArgumentException(
                        "A capability-selection failure outcome cannot be null.",
                        nameof(outcomes));
                }

                if (!integrationIds.Add(outcome.IntegrationId))
                {
                    throw new ArgumentException(
                        "A capability-selection failure cannot repeat an " +
                        "external-mod integration outcome.",
                        nameof(outcomes));
                }

                copied.Add(outcome);
            }

            return new ReadOnlyCollection<ExternalModIntegrationOutcome>(copied);
        }
    }
}
