#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Immutable, engine-independent evidence used to refine ONI's original
    /// clearable-destination decision with temperature eligibility.
    /// </summary>
    /// <remarks>
    /// Each boolean names a proved fact rather than a nullable lookup result. This
    /// prevents adapter failures from being confused with a valid negative
    /// eligibility result and makes the conservative fallback policy explicit.
    /// </remarks>
    internal readonly struct ClearableDestinationSweepEligibilityInput
    {
        internal ClearableDestinationSweepEligibilityInput(
            bool originalHasDestination,
            int enabledTemperatureConstraintCount,
            bool hasPrimaryElement,
            bool isParentWorldResolved,
            bool isEligibilitySnapshotCurrent,
            bool currentEligibilityAllowsPickup)
        {
            if (enabledTemperatureConstraintCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(enabledTemperatureConstraintCount),
                    "The enabled temperature-constraint count cannot be " +
                    "negative.");
            }

            OriginalHasDestination = originalHasDestination;
            EnabledTemperatureConstraintCount =
                enabledTemperatureConstraintCount;
            HasPrimaryElement = hasPrimaryElement;
            IsParentWorldResolved = isParentWorldResolved;
            IsEligibilitySnapshotCurrent = isEligibilitySnapshotCurrent;
            CurrentEligibilityAllowsPickup = currentEligibilityAllowsPickup;
        }

        internal bool OriginalHasDestination { get; }

        internal int EnabledTemperatureConstraintCount { get; }

        internal bool HasPrimaryElement { get; }

        internal bool IsParentWorldResolved { get; }

        internal bool IsEligibilitySnapshotCurrent { get; }

        internal bool CurrentEligibilityAllowsPickup { get; }
    }
}
