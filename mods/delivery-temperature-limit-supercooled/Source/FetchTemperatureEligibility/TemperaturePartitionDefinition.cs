#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Immutable endpoint partition scoped to one pickup-update grouping session.
    /// </summary>
    internal sealed class TemperaturePartitionDefinition
    {
        private readonly int[] sortedDecisionEndpointsKelvin;
        private readonly ReadOnlyCollection<int> readOnlySortedDecisionEndpointsKelvin;

        private TemperaturePartitionDefinition(
            int definitionId,
            int[] sortedDecisionEndpointsKelvin)
        {
            DefinitionId = definitionId;
            this.sortedDecisionEndpointsKelvin = sortedDecisionEndpointsKelvin;
            readOnlySortedDecisionEndpointsKelvin =
                Array.AsReadOnly(sortedDecisionEndpointsKelvin);
        }

        internal int DefinitionId { get; }

        internal IReadOnlyList<int> SortedDecisionEndpointsKelvin =>
            readOnlySortedDecisionEndpointsKelvin;

        internal static TemperaturePartitionDefinition Create(
            int definitionId,
            IReadOnlyList<int> decisionEndpointsKelvin)
        {
            if (definitionId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definitionId),
                    definitionId,
                    "A temperature partition definition ID must be positive " +
                    "within its owning pickup-update grouping session.");
            }

            if (decisionEndpointsKelvin == null)
            {
                throw new ArgumentNullException(nameof(decisionEndpointsKelvin));
            }

            if (decisionEndpointsKelvin.Count == 0)
            {
                throw new ArgumentException(
                    "decisionEndpointsKelvin must contain at least one applicable " +
                    "temperature decision endpoint. Use the explicit " +
                    "no-temperature-distinction class when the union is empty.",
                    nameof(decisionEndpointsKelvin));
            }

            // Copy before sorting so the published definition never retains or
            // mutates caller-owned storage. Endpoints include both configurable
            // bounds: 0 separates below-range from 0 K, while ONI's maximum
            // separates the greatest ordinary bucket from the upper sentinel.
            var normalizedEndpointsKelvin =
                new int[decisionEndpointsKelvin.Count];
            for (int endpointIndex = 0;
                 endpointIndex < decisionEndpointsKelvin.Count;
                 endpointIndex++)
            {
                int endpointKelvin = decisionEndpointsKelvin[endpointIndex];
                if (endpointKelvin <
                        OniStorableTemperatureBounds.MinimumTemperatureKelvin ||
                    endpointKelvin >
                        OniStorableTemperatureBounds.MaximumTemperatureKelvin)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(decisionEndpointsKelvin),
                        endpointKelvin,
                        "Every decision endpoint must be within ONI's " +
                        "configurable storable-temperature range, inclusively.");
                }

                normalizedEndpointsKelvin[endpointIndex] = endpointKelvin;
            }

            Array.Sort(normalizedEndpointsKelvin);

            int uniqueEndpointCount = 1;
            for (int endpointIndex = 1;
                 endpointIndex < normalizedEndpointsKelvin.Length;
                 endpointIndex++)
            {
                if (normalizedEndpointsKelvin[endpointIndex] ==
                    normalizedEndpointsKelvin[uniqueEndpointCount - 1])
                {
                    continue;
                }

                normalizedEndpointsKelvin[uniqueEndpointCount] =
                    normalizedEndpointsKelvin[endpointIndex];
                uniqueEndpointCount++;
            }

            if (uniqueEndpointCount != normalizedEndpointsKelvin.Length)
            {
                var exactNormalizedEndpointsKelvin =
                    new int[uniqueEndpointCount];
                Array.Copy(
                    normalizedEndpointsKelvin,
                    exactNormalizedEndpointsKelvin,
                    uniqueEndpointCount);
                normalizedEndpointsKelvin = exactNormalizedEndpointsKelvin;
            }

            return new TemperaturePartitionDefinition(
                definitionId,
                normalizedEndpointsKelvin);
        }

        internal int Classify(TemperatureDecisionBucket bucket)
        {
            if (bucket.IsBelowMinimumKelvin)
            {
                return 0;
            }

            int decisionTemperatureKelvin;
            if (bucket.IsAtOrAboveMaximumKelvin)
            {
                decisionTemperatureKelvin =
                    OniStorableTemperatureBounds.MaximumTemperatureKelvin;
            }
            else if (!bucket.TryGetIntegerKelvin(out decisionTemperatureKelvin))
            {
                // TemperatureDecisionBucket currently makes this unreachable. Keep
                // the guard defensive so a future bucket kind cannot silently be
                // classified with a fabricated temperature value.
                throw new ArgumentOutOfRangeException(
                    nameof(bucket),
                    "The temperature decision bucket has no classifiable value.");
            }

            // Upper-bound search returns the number of endpoints less than or equal
            // to the decision temperature. This is exactly the interval ordinal and
            // keeps the per-pickup path logarithmic in the relevant endpoint count.
            int lowerEndpointIndex = 0;
            int upperEndpointIndex = sortedDecisionEndpointsKelvin.Length;
            while (lowerEndpointIndex < upperEndpointIndex)
            {
                int middleEndpointIndex = lowerEndpointIndex +
                    ((upperEndpointIndex - lowerEndpointIndex) / 2);
                if (sortedDecisionEndpointsKelvin[middleEndpointIndex] <=
                    decisionTemperatureKelvin)
                {
                    lowerEndpointIndex = middleEndpointIndex + 1;
                }
                else
                {
                    upperEndpointIndex = middleEndpointIndex;
                }
            }

            return lowerEndpointIndex;
        }
    }
}
