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
        private readonly Lazy<PreparedRuntimeAuthorityContribution>?
            selectedContributionPreparation;

        private RuntimeCapabilitySelectionEntry(
            RuntimeCapabilityDefinition definition,
            Lazy<PreparedRuntimeAuthorityContribution>?
                selectedContributionPreparation,
            RuntimeAuthorityImplementationIdentity?
                selectedImplementationIdentity,
            IntegrationCapabilityDisposition disposition,
            string? diagnosticCode,
            string? diagnosticMessage)
        {
            Definition = definition;
            this.selectedContributionPreparation =
                selectedContributionPreparation;
            SelectedImplementationIdentity = selectedImplementationIdentity;
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
                new Lazy<PreparedRuntimeAuthorityContribution>(() =>
                    selectedContribution),
                selectedContribution.ImplementationIdentity,
                IntegrationCapabilityDisposition.Selected,
                null,
                null);
        }

        internal static RuntimeCapabilitySelectionEntry
            ForSelectedKleiBaseline(RuntimeCapabilityDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (!definition.HasKleiBaselineContribution)
            {
                throw new ArgumentException(
                    "A selected Klei baseline requires a declared baseline " +
                    "preparation.",
                    nameof(definition));
            }

            return new RuntimeCapabilitySelectionEntry(
                definition,
                new Lazy<PreparedRuntimeAuthorityContribution>(() =>
                    definition.PrepareKleiBaselineContribution()),
                RuntimeAuthorityImplementationIdentity.KleiBaseline,
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
                null,
                IntegrationCapabilityDisposition.Unavailable,
                validatedDiagnosticCode,
                validatedDiagnosticMessage);
        }

        internal RuntimeCapabilityDefinition Definition { get; }

        internal RuntimeCapabilityId CapabilityId => Definition.Id;

        internal bool HasSelectedContribution =>
            selectedContributionPreparation != null;

        internal PreparedRuntimeAuthorityContribution
            PrepareSelectedContribution()
        {
            if (selectedContributionPreparation == null)
            {
                throw new InvalidOperationException(
                    "This runtime capability selection has no contribution " +
                    "preparation.");
            }

            return selectedContributionPreparation.Value;
        }

        internal RuntimeAuthorityImplementationIdentity?
            SelectedImplementationIdentity { get; }

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
            ExternalModIntegrationOutcomes = new ReadOnlyCollection<
                ExternalModIntegrationOutcome>(copiedOutcomes);
            selectionsByCapability = new ReadOnlyDictionary<
                RuntimeCapabilityId,
                RuntimeCapabilitySelectionEntry>(byCapability);
        }

        internal IReadOnlyList<RuntimeCapabilitySelectionEntry>
            CapabilitySelections { get; }

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
