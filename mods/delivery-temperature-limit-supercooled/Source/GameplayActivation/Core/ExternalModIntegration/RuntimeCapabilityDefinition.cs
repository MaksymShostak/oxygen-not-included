#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    internal enum RuntimeCapabilityCriticality
    {
        Required,
        Optional
    }

    /// <summary>
    /// Stable identity of a set of capabilities that must resolve to one
    /// coherent implementation family.
    /// </summary>
    internal readonly struct RuntimeCapabilityBundleId :
        IEquatable<RuntimeCapabilityBundleId>
    {
        internal RuntimeCapabilityBundleId(string value)
        {
            Value = ValidatedIntegrationIdentifier.RequireKebabCase(
                value,
                nameof(value));
        }

        internal string Value { get; }

        public bool Equals(RuntimeCapabilityBundleId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object? obj) =>
            obj is RuntimeCapabilityBundleId other && Equals(other);

        public override int GetHashCode() =>
            Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value ?? string.Empty;
    }

    /// <summary>
    /// Declares Temperature Limit's policy for one semantic runtime capability.
    /// External integrations may supply authority evidence, but cannot change
    /// this criticality or the coherence bundle.
    /// </summary>
    internal sealed class RuntimeCapabilityDefinition
    {
        private readonly Lazy<PreparedRuntimeAuthorityContribution>?
            kleiBaselineContributionPreparation;

        internal RuntimeCapabilityDefinition(
            RuntimeCapabilityId id,
            RuntimeCapabilityCriticality criticality,
            Func<PreparedRuntimeAuthorityContribution>?
                prepareKleiBaselineContribution,
            RuntimeCapabilityBundleId? atomicBundleId)
        {
            if (string.IsNullOrEmpty(id.Value))
            {
                throw new ArgumentException(
                    "A runtime capability definition requires a valid identity.",
                    nameof(id));
            }

            if (!Enum.IsDefined(typeof(RuntimeCapabilityCriticality), criticality))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(criticality),
                    criticality,
                    "Unknown runtime capability criticality.");
            }

            if (atomicBundleId.HasValue &&
                string.IsNullOrEmpty(atomicBundleId.Value.Value))
            {
                throw new ArgumentException(
                    "An atomic capability bundle requires a valid identity.",
                    nameof(atomicBundleId));
            }

            Id = id;
            Criticality = criticality;
            kleiBaselineContributionPreparation =
                prepareKleiBaselineContribution == null
                    ? null
                    : new Lazy<PreparedRuntimeAuthorityContribution>(() =>
                        ValidateKleiBaselineContribution(
                            id,
                            prepareKleiBaselineContribution()));
            AtomicBundleId = atomicBundleId;
        }

        internal RuntimeCapabilityId Id { get; }

        internal RuntimeCapabilityCriticality Criticality { get; }

        internal bool IsRequired =>
            Criticality == RuntimeCapabilityCriticality.Required;

        internal bool HasKleiBaselineContribution =>
            kleiBaselineContributionPreparation != null;

        internal PreparedRuntimeAuthorityContribution
            PrepareKleiBaselineContribution()
        {
            if (kleiBaselineContributionPreparation == null)
            {
                throw new InvalidOperationException(
                    "This runtime capability has no Klei baseline " +
                    "preparation.");
            }

            return kleiBaselineContributionPreparation.Value;
        }

        internal RuntimeCapabilityBundleId? AtomicBundleId { get; }

        private static PreparedRuntimeAuthorityContribution
            ValidateKleiBaselineContribution(
                RuntimeCapabilityId capabilityId,
                PreparedRuntimeAuthorityContribution? contribution)
        {
            if (contribution == null)
            {
                throw new InvalidOperationException(
                    "Klei baseline contribution preparation returned null.");
            }

            if (!contribution.CapabilityId.Equals(capabilityId))
            {
                throw new ArgumentException(
                    "The Klei baseline contribution must implement the " +
                    "capability being defined.",
                    nameof(contribution));
            }

            if (contribution.AuthorityObservation !=
                RuntimeAuthorityObservation.OwnsCompatible)
            {
                throw new ArgumentException(
                    "A Klei baseline must be a complete compatible " +
                    "runtime-authority contribution.",
                    nameof(contribution));
            }

            if (!contribution.ImplementationIdentity.IsKleiBaseline)
            {
                throw new ArgumentException(
                    "A Klei baseline contribution must identify the built-in " +
                    "Klei implementation.",
                    nameof(contribution));
            }

            for (int requirementIndex = 0;
                 requirementIndex < contribution.AuthorityRequirements.Count;
                 requirementIndex++)
            {
                if (contribution.AuthorityRequirements[requirementIndex].Kind !=
                    RuntimeAuthorityRequirementKind.KleiOriginal)
                {
                    throw new ArgumentException(
                        "A Klei baseline contribution may require only " +
                        "Klei-original runtime authority.",
                        nameof(contribution));
                }
            }

            return contribution;
        }
    }
}
