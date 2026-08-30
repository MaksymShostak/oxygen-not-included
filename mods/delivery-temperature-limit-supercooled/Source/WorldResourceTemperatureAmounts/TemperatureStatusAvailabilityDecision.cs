#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Characterizes the existing status-item availability adjustment while
    /// keeping inventory completeness explicit and independent of Unity.
    /// </summary>
    internal static class TemperatureStatusAvailabilityDecision
    {
        /// <summary>
        /// Returns whether the original ONI availability already reaches the
        /// requested minimum and therefore warrants a temperature-aware query.
        /// </summary>
        /// <remarks>
        /// This deliberately negates the original less-than early-exit condition
        /// instead of rewriting it as greater-than-or-equal. The two expressions
        /// differ for NaN, and this decision must characterize the existing branch
        /// rather than silently narrow the inputs that reach the status hook.
        /// </remarks>
        internal static bool ShouldTryReplacement(
            float originalStorageAmount,
            float originalFetchableAmount,
            float minimumRequiredAmount) =>
            !(originalStorageAmount + originalFetchableAmount <
              minimumRequiredAmount);

        /// <summary>
        /// Applies the exact pre-rewrite status arithmetic once a complete
        /// temperature-eligible total has been proven.
        /// </summary>
        internal static float CalculateFetchable(
            float eligibleTotal,
            float remaining) =>
            eligibleTotal + Math.Min(remaining, eligibleTotal);

        /// <summary>
        /// Produces a replacement only from complete inventory evidence.
        /// Callers must leave their original fetchable amount untouched when this
        /// method returns false; the out value then has no domain meaning.
        /// </summary>
        internal static bool TryCalculateReplacementFetchable(
            TemperatureConstrainedAmountAvailability availability,
            float remaining,
            out float replacementFetchable)
        {
            switch (availability.State)
            {
                case TemperatureConstrainedAmountAvailabilityState
                    .TemperatureConstraintDisabled:
                    replacementFetchable = 0.0f;
                    return false;

                case TemperatureConstrainedAmountAvailabilityState
                    .InventoryIncomplete:
                    replacementFetchable = 0.0f;
                    return false;

                case TemperatureConstrainedAmountAvailabilityState.Complete:
                    if (!availability.TryGetCompleteAvailableAmount(
                            out var completeEligibleAmount))
                    {
                        // The guarded result currently makes this impossible. Keep
                        // the check at the ownership boundary so a future result-
                        // contract change cannot turn complete into a false zero.
                        throw new InvalidOperationException(
                            "Complete temperature-constrained availability did " +
                            "not expose its complete available amount.");
                    }

                    replacementFetchable = CalculateFetchable(
                        completeEligibleAmount,
                        remaining);
                    return true;
            }

            // Do not add a default case. Every named state is handled above, and a
            // future enum member must fail loudly until its semantics are designed.
            throw new ArgumentOutOfRangeException(
                nameof(availability),
                availability.State,
                "Unknown temperature-constrained availability state.");
        }
    }
}
