#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Ordered immutable output of catalog-declared external-mod preparation.
    /// Runtime contributions and sanitized outcomes remain separate by design.
    /// </summary>
    internal sealed class DeclaredIntegrationPreparationResult
    {
        internal DeclaredIntegrationPreparationResult(
            IEnumerable<PreparedRuntimeAuthorityContribution>
                runtimeAuthorityContributions,
            IEnumerable<ExternalModIntegrationOutcome>
                externalModIntegrationOutcomes)
        {
            RuntimeAuthorityContributions = CopyContributions(
                runtimeAuthorityContributions);
            ExternalModIntegrationOutcomes = CopyOutcomes(
                externalModIntegrationOutcomes);
            ValidateContributionOutcomes();
        }

        internal IReadOnlyList<PreparedRuntimeAuthorityContribution>
            RuntimeAuthorityContributions { get; }

        internal IReadOnlyList<ExternalModIntegrationOutcome>
            ExternalModIntegrationOutcomes { get; }

        private static IReadOnlyList<PreparedRuntimeAuthorityContribution>
            CopyContributions(
                IEnumerable<PreparedRuntimeAuthorityContribution> contributions)
        {
            if (contributions == null)
            {
                throw new ArgumentNullException(nameof(contributions));
            }

            var copied = new List<PreparedRuntimeAuthorityContribution>();
            var keys = new HashSet<(
                RuntimeAuthorityImplementationIdentity ImplementationIdentity,
                RuntimeCapabilityId CapabilityId)>();
            foreach (PreparedRuntimeAuthorityContribution contribution in
                     contributions)
            {
                if (contribution == null)
                {
                    throw new ArgumentException(
                        "A declared preparation contribution cannot be null.",
                        nameof(contributions));
                }

                if (!keys.Add((
                        contribution.ImplementationIdentity,
                        contribution.CapabilityId)))
                {
                    throw new ArgumentException(
                        "Declared preparation cannot repeat one integration's " +
                        "capability contribution.",
                        nameof(contributions));
                }

                copied.Add(contribution);
            }

            return new ReadOnlyCollection<PreparedRuntimeAuthorityContribution>(
                copied);
        }

        private static IReadOnlyList<ExternalModIntegrationOutcome> CopyOutcomes(
            IEnumerable<ExternalModIntegrationOutcome> outcomes)
        {
            if (outcomes == null)
            {
                throw new ArgumentNullException(nameof(outcomes));
            }

            var copied = new List<ExternalModIntegrationOutcome>();
            var ids = new HashSet<DeclaredModIntegrationId>();
            foreach (ExternalModIntegrationOutcome outcome in outcomes)
            {
                if (outcome == null)
                {
                    throw new ArgumentException(
                        "A declared integration outcome cannot be null.",
                        nameof(outcomes));
                }

                if (!ids.Add(outcome.IntegrationId))
                {
                    throw new ArgumentException(
                        "Declared preparation cannot repeat an integration outcome.",
                        nameof(outcomes));
                }

                copied.Add(outcome);
            }

            return new ReadOnlyCollection<ExternalModIntegrationOutcome>(copied);
        }

        private void ValidateContributionOutcomes()
        {
            var outcomeIds = new HashSet<DeclaredModIntegrationId>();
            for (int index = 0;
                 index < ExternalModIntegrationOutcomes.Count;
                 index++)
            {
                outcomeIds.Add(
                    ExternalModIntegrationOutcomes[index].IntegrationId);
            }

            for (int index = 0;
                 index < RuntimeAuthorityContributions.Count;
                 index++)
            {
                RuntimeAuthorityImplementationIdentity implementationIdentity =
                    RuntimeAuthorityContributions[index]
                        .ImplementationIdentity;
                DeclaredModIntegrationId? externalIntegrationId =
                    implementationIdentity.DeclaredExternalIntegrationId;
                if (!externalIntegrationId.HasValue ||
                    !outcomeIds.Contains(externalIntegrationId.Value))
                {
                    throw new ArgumentException(
                        "Declared preparation accepts only external runtime-" +
                        "authority contributions with their sanitized " +
                        "integration outcome.");
                }
            }
        }
    }
}
