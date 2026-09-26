#nullable enable

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Determines whether a candidate fetch chore may be coalesced into a root
    /// fetch chore without delivering a pickup the candidate would reject.
    /// </summary>
    /// <remarks>
    /// This domain operation compares immutable configured behavior, never Unity
    /// component reference identity. ONI's FetchAreaChore selects every pickup
    /// against the root chore only and then stores those pickups in each
    /// coalesced candidate's destination without a temperature recheck. A
    /// missing or disabled candidate retains ONI's characterized permissive
    /// coalescing behavior because it contributes no temperature-specific
    /// requirement. Once the candidate is constrained, the root's admitted
    /// interval must be a subset of the candidate's admitted interval.
    /// </remarks>
    internal static class FetchChoreTemperatureConstraintContainment
    {
        internal static bool CanCombine(
            DeliveryTemperatureConstraint? rootConstraint,
            DeliveryTemperatureConstraint? candidateConstraint)
        {
            if (!candidateConstraint.HasValue ||
                !candidateConstraint.Value.IsEnabled)
            {
                return true;
            }

            if (!rootConstraint.HasValue ||
                !rootConstraint.Value.IsEnabled)
            {
                // An unconstrained root admits every pickup temperature, so it
                // cannot be contained by a constrained candidate.
                return false;
            }

            DeliveryTemperatureConstraint enabledCandidateConstraint =
                candidateConstraint.Value;
            if (enabledCandidateConstraint.IsEmpty)
            {
                // An empty candidate rejects every pickup the root may select.
                return false;
            }

            DeliveryTemperatureConstraint enabledRootConstraint =
                rootConstraint.Value;
            if (enabledRootConstraint.IsEmpty)
            {
                // An empty root is vacuously contained, but it cannot select a
                // legitimate pickup either. Rejecting keeps any root pickup that
                // bypassed the root filter out of a constrained candidate.
                return false;
            }

            return enabledCandidateConstraint.MinimumInclusiveKelvin <=
                    enabledRootConstraint.MinimumInclusiveKelvin &&
                enabledRootConstraint.MaximumExclusiveKelvin <=
                    enabledCandidateConstraint.MaximumExclusiveKelvin;
        }
    }
}
