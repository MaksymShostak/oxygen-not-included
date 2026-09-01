#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Immutable output of one declared runtime-authority inspector: the
    /// sanitized report projection plus any complete capability contributions.
    /// </summary>
    internal sealed class PreparedRuntimeAuthorityInspection
    {
        internal PreparedRuntimeAuthorityInspection(
            ExternalModIntegrationOutcome outcome,
            IEnumerable<PreparedRuntimeAuthorityContribution> contributions)
        {
            Outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
            Contributions = CopyContributions(outcome, contributions);
        }

        internal ExternalModIntegrationOutcome Outcome { get; }

        internal IReadOnlyList<PreparedRuntimeAuthorityContribution>
            Contributions { get; }

        private static IReadOnlyList<PreparedRuntimeAuthorityContribution>
            CopyContributions(
                ExternalModIntegrationOutcome outcome,
                IEnumerable<PreparedRuntimeAuthorityContribution> contributions)
        {
            if (contributions == null)
            {
                throw new ArgumentNullException(nameof(contributions));
            }

            var outcomeCapabilities = new Dictionary<
                RuntimeCapabilityId,
                ExternalModIntegrationCapabilityOutcome>();
            for (int index = 0; index < outcome.Capabilities.Count; index++)
            {
                ExternalModIntegrationCapabilityOutcome capabilityOutcome =
                    outcome.Capabilities[index];
                outcomeCapabilities.Add(
                    capabilityOutcome.CapabilityId,
                    capabilityOutcome);
            }

            var copied = new List<PreparedRuntimeAuthorityContribution>();
            var seenCapabilities = new HashSet<RuntimeCapabilityId>();
            foreach (PreparedRuntimeAuthorityContribution contribution in
                     contributions)
            {
                if (contribution == null)
                {
                    throw new ArgumentException(
                        "A prepared runtime-authority contribution cannot be null.",
                        nameof(contributions));
                }

                RuntimeAuthorityImplementationIdentity expectedIdentity =
                    RuntimeAuthorityImplementationIdentity
                        .ForDeclaredExternalIntegration(outcome.IntegrationId);
                if (!contribution.ImplementationIdentity.Equals(
                        expectedIdentity))
                {
                    throw new ArgumentException(
                        "Every prepared contribution must identify the " +
                        "declared external integration outcome returned by the " +
                        "same inspector.",
                        nameof(contributions));
                }

                if (!seenCapabilities.Add(contribution.CapabilityId))
                {
                    throw new ArgumentException(
                        "One inspector cannot return two contributions for the " +
                        "same capability.",
                        nameof(contributions));
                }

                ExternalModIntegrationCapabilityOutcome? capabilityOutcome;
                if (!outcomeCapabilities.TryGetValue(
                        contribution.CapabilityId,
                        out capabilityOutcome))
                {
                    throw new ArgumentException(
                        "Every prepared contribution requires a corresponding " +
                        "sanitized capability outcome.",
                        nameof(contributions));
                }

                if (capabilityOutcome.AuthorityObservation !=
                    contribution.AuthorityObservation)
                {
                    throw new ArgumentException(
                        "A prepared contribution's authority observation must " +
                        "exactly match its sanitized capability outcome.",
                        nameof(contributions));
                }

                copied.Add(contribution);
            }

            foreach (ExternalModIntegrationCapabilityOutcome capabilityOutcome in
                     outcomeCapabilities.Values)
            {
                if (capabilityOutcome.AuthorityObservation !=
                        RuntimeAuthorityObservation.DoesNotOwn &&
                    !seenCapabilities.Contains(capabilityOutcome.CapabilityId))
                {
                    throw new ArgumentException(
                        "Every owning or unavailable runtime-authority " +
                        "observation requires a corresponding prepared " +
                        "contribution.",
                        nameof(contributions));
                }
            }

            return new ReadOnlyCollection<PreparedRuntimeAuthorityContribution>(
                copied);
        }
    }
}
