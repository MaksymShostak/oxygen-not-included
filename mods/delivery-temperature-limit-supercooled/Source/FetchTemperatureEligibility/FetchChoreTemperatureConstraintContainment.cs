#nullable enable

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Determines whether a candidate fetch chore may be coalesced into a root
    /// fetch chore without broadening the root's enabled temperature interval.
    /// </summary>
    /// <remarks>
    /// This domain operation compares immutable configured behavior, never Unity
    /// component reference identity. A missing or disabled candidate retains
    /// ONI's characterized permissive coalescing behavior because it contributes
    /// no temperature-specific requirement. Once the candidate is constrained,
    /// its admitted interval must be a subset of the root's admitted interval.
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
                return false;
            }

            DeliveryTemperatureConstraint enabledCandidateConstraint =
                candidateConstraint.Value;
            if (enabledCandidateConstraint.IsEmpty)
            {
                // The empty set is contained by every root because it cannot
                // admit a pickup that the root would reject.
                return true;
            }

            DeliveryTemperatureConstraint enabledRootConstraint =
                rootConstraint.Value;
            if (enabledRootConstraint.IsEmpty)
            {
                // A nonempty candidate cannot be contained by an empty root.
                return false;
            }

            return enabledRootConstraint.MinimumInclusiveKelvin <=
                    enabledCandidateConstraint.MinimumInclusiveKelvin &&
                enabledCandidateConstraint.MaximumExclusiveKelvin <=
                    enabledRootConstraint.MaximumExclusiveKelvin;
        }
    }
}
