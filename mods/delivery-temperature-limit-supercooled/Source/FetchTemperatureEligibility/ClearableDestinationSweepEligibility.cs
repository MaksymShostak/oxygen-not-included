#nullable enable

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Applies the fail-closed temperature refinement to ONI's authoritative
    /// clearable-destination result.
    /// </summary>
    internal static class ClearableDestinationSweepEligibility
    {
        internal static bool AllowsClearing(
            ClearableDestinationSweepEligibilityInput input)
        {
            if (!input.OriginalHasDestination)
            {
                // This feature may only narrow Klei's decision; it can never create
                // a destination which the authoritative game logic did not find.
                return false;
            }

            if (input.EnabledTemperatureConstraintCount == 0)
            {
                // The overwhelmingly common inactive path preserves Klei's answer
                // without requiring element, topology, or eligibility evidence.
                return true;
            }

            if (!input.HasPrimaryElement ||
                !input.IsParentWorldResolved ||
                !input.IsEligibilitySnapshotCurrent)
            {
                // Missing or stale evidence must not authorize a sweep that could
                // have no temperature-compatible destination.
                return false;
            }

            return input.CurrentEligibilityAllowsPickup;
        }
    }
}
