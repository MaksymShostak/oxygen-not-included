#nullable enable

using System;
using System.Collections.Generic;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Complete immutable facts required by active-constraint consumers.
    /// Per-component constraints deliberately remain in the token-owned registry;
    /// copying them here would make bulk spawn and save-load churn quadratic.
    /// </summary>
    internal sealed class ActiveTemperatureConstraintSnapshot
    {
        internal ActiveTemperatureConstraintSnapshot(
            TemperatureConstraintGeneration generation,
            int enabledConstraintCount,
            int enabledNonEmptyConstraintCount,
            IReadOnlyList<int> sortedDecisionEndpointsKelvin)
        {
            if (enabledConstraintCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(enabledConstraintCount));
            }

            if (enabledNonEmptyConstraintCount < 0 ||
                enabledNonEmptyConstraintCount > enabledConstraintCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(enabledNonEmptyConstraintCount));
            }

            Generation = generation;
            EnabledConstraintCount = enabledConstraintCount;
            EnabledNonEmptyConstraintCount = enabledNonEmptyConstraintCount;
            SortedDecisionEndpointsKelvin = sortedDecisionEndpointsKelvin ??
                throw new ArgumentNullException(nameof(sortedDecisionEndpointsKelvin));
        }

        internal TemperatureConstraintGeneration Generation { get; }

        internal int EnabledConstraintCount { get; }

        internal int EnabledNonEmptyConstraintCount { get; }

        // The registry supplies a read-only wrapper around its private array. The
        // caller therefore cannot cast this contract back to a mutable owned array.
        internal IReadOnlyList<int> SortedDecisionEndpointsKelvin { get; }
    }
}
