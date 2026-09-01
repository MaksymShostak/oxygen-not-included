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
        internal RuntimeCapabilityDefinition(
            RuntimeCapabilityId id,
            RuntimeCapabilityCriticality criticality,
            PreparedRuntimeAuthorityContribution? kleiBaselineContribution,
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

            if (kleiBaselineContribution != null)
            {
                if (!kleiBaselineContribution.CapabilityId.Equals(id))
                {
                    throw new ArgumentException(
                        "The Klei baseline contribution must implement the " +
                        "capability being defined.",
                        nameof(kleiBaselineContribution));
                }

                if (kleiBaselineContribution.AuthorityObservation !=
                    RuntimeAuthorityObservation.OwnsCompatible)
                {
                    throw new ArgumentException(
                        "A Klei baseline must be a complete compatible " +
                        "runtime-authority contribution.",
                        nameof(kleiBaselineContribution));
                }

                if (!kleiBaselineContribution.ImplementationIdentity
                        .IsKleiBaseline)
                {
                    throw new ArgumentException(
                        "A Klei baseline contribution must identify the " +
                        "built-in Klei implementation.",
                        nameof(kleiBaselineContribution));
                }

                for (int requirementIndex = 0;
                     requirementIndex <
                        kleiBaselineContribution.AuthorityRequirements.Count;
                     requirementIndex++)
                {
                    if (kleiBaselineContribution
                            .AuthorityRequirements[requirementIndex]
                            .Kind != RuntimeAuthorityRequirementKind.KleiOriginal)
                    {
                        throw new ArgumentException(
                            "A Klei baseline contribution may require only " +
                            "Klei-original runtime authority.",
                            nameof(kleiBaselineContribution));
                    }
                }
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
            KleiBaselineContribution = kleiBaselineContribution;
            AtomicBundleId = atomicBundleId;
        }

        internal RuntimeCapabilityId Id { get; }

        internal RuntimeCapabilityCriticality Criticality { get; }

        internal bool IsRequired =>
            Criticality == RuntimeCapabilityCriticality.Required;

        internal PreparedRuntimeAuthorityContribution?
            KleiBaselineContribution { get; }

        internal RuntimeCapabilityBundleId? AtomicBundleId { get; }
    }
}
