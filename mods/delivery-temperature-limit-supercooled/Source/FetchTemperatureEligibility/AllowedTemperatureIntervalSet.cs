#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Immutable normalized union of destination temperature eligibility.
    /// </summary>
    internal sealed class AllowedTemperatureIntervalSet
    {
        private static readonly ReadOnlyCollection<AllowedTemperatureInterval>
            EmptyIntervals = Array.AsReadOnly(
                Array.Empty<AllowedTemperatureInterval>());
        private static readonly AllowedTemperatureIntervalSet
            AllowsNoTemperatureInstance =
                new AllowedTemperatureIntervalSet(
                    AllowedTemperatureIntervalSetState.AllowsNoTemperature,
                    Array.Empty<AllowedTemperatureInterval>());
        private static readonly AllowedTemperatureIntervalSet
            AllowsEveryTemperatureInstance =
                new AllowedTemperatureIntervalSet(
                    AllowedTemperatureIntervalSetState.AllowsEveryTemperature,
                    intervals: null);

        // AllowsEveryTemperature deliberately carries no interval array. A fake
        // [0, 10000) interval would reject the two sentinel decision buckets even
        // though an unconstrained destination admits them.
        private readonly AllowedTemperatureInterval[]? intervals;
        private readonly ReadOnlyCollection<AllowedTemperatureInterval>?
            readOnlyIntervals;
        private readonly AllowedTemperatureIntervalSetState state;

        private AllowedTemperatureIntervalSet(
            AllowedTemperatureIntervalSetState state,
            AllowedTemperatureInterval[]? intervals)
        {
            this.state = state;
            this.intervals = intervals;
            readOnlyIntervals = intervals == null
                ? null
                : Array.AsReadOnly(intervals);
        }

        internal bool AllowsNoTemperature =>
            state == AllowedTemperatureIntervalSetState.AllowsNoTemperature;

        internal bool AllowsEveryTemperature =>
            state == AllowedTemperatureIntervalSetState.AllowsEveryTemperature;

        internal IReadOnlyList<AllowedTemperatureInterval> Intervals =>
            readOnlyIntervals ?? EmptyIntervals;

        internal static AllowedTemperatureIntervalSet CreateFromDestinations(
            bool includesUnconstrainedDestination,
            IReadOnlyList<DeliveryTemperatureConstraint>
                enabledDestinationConstraints)
        {
            if (enabledDestinationConstraints == null)
            {
                throw new ArgumentNullException(
                    nameof(enabledDestinationConstraints));
            }

            // Validate the enabled-only contract before honoring the dominant
            // unconstrained state. This preserves defensive argument checking
            // while avoiding any interval allocation when every temperature is
            // already known to be eligible.
            for (int constraintIndex = 0;
                 constraintIndex < enabledDestinationConstraints.Count;
                 constraintIndex++)
            {
                DeliveryTemperatureConstraint constraint =
                    enabledDestinationConstraints[constraintIndex];
                if (!constraint.IsEnabled)
                {
                    throw new ArgumentException(
                        "enabledDestinationConstraints may contain only enabled " +
                        "delivery temperature constraints; represent an " +
                        "unconstrained destination with the separately named " +
                        "includesUnconstrainedDestination argument.",
                        nameof(enabledDestinationConstraints));
                }

            }

            if (includesUnconstrainedDestination)
            {
                return AllowsEveryTemperatureInstance;
            }

            var contributedIntervals =
                new List<AllowedTemperatureInterval>(
                    enabledDestinationConstraints.Count);
            for (int constraintIndex = 0;
                 constraintIndex < enabledDestinationConstraints.Count;
                 constraintIndex++)
            {
                DeliveryTemperatureConstraint constraint =
                    enabledDestinationConstraints[constraintIndex];
                if (!constraint.IsEmpty)
                {
                    contributedIntervals.Add(
                        new AllowedTemperatureInterval(
                            constraint.MinimumInclusiveKelvin,
                            constraint.MaximumExclusiveKelvin));
                }
            }

            if (contributedIntervals.Count == 0)
            {
                return AllowsNoTemperatureInstance;
            }

            contributedIntervals.Sort(
                AllowedTemperatureIntervalComparer.Instance);
            var mergedIntervals =
                new AllowedTemperatureInterval[contributedIntervals.Count];
            int mergedIntervalCount = 0;
            AllowedTemperatureInterval currentInterval =
                contributedIntervals[0];
            for (int contributedIntervalIndex = 1;
                 contributedIntervalIndex < contributedIntervals.Count;
                 contributedIntervalIndex++)
            {
                AllowedTemperatureInterval nextInterval =
                    contributedIntervals[contributedIntervalIndex];
                if (nextInterval.MinimumInclusiveKelvin <=
                    currentInterval.MaximumExclusiveKelvin)
                {
                    // Overlap and adjacency are behaviorally identical for integer
                    // buckets, so both deliberately normalize into one interval.
                    currentInterval = new AllowedTemperatureInterval(
                        currentInterval.MinimumInclusiveKelvin,
                        Math.Max(
                            currentInterval.MaximumExclusiveKelvin,
                            nextInterval.MaximumExclusiveKelvin));
                }
                else
                {
                    mergedIntervals[mergedIntervalCount] = currentInterval;
                    mergedIntervalCount++;
                    currentInterval = nextInterval;
                }
            }

            mergedIntervals[mergedIntervalCount] = currentInterval;
            mergedIntervalCount++;
            if (mergedIntervalCount != mergedIntervals.Length)
            {
                var exactMergedIntervals =
                    new AllowedTemperatureInterval[mergedIntervalCount];
                Array.Copy(
                    mergedIntervals,
                    exactMergedIntervals,
                    mergedIntervalCount);
                mergedIntervals = exactMergedIntervals;
            }

            return new AllowedTemperatureIntervalSet(
                AllowedTemperatureIntervalSetState.FiniteIntervals,
                mergedIntervals);
        }

        internal bool Allows(TemperatureDecisionBucket bucket)
        {
            if (AllowsEveryTemperature)
            {
                return true;
            }

            if (AllowsNoTemperature ||
                !bucket.TryGetIntegerKelvin(out int integerKelvin))
            {
                return false;
            }

            // Find the first interval whose minimum is greater than the bucket;
            // only the immediately preceding interval can contain it.
            int lowerIntervalIndex = 0;
            int upperIntervalIndex = intervals!.Length;
            while (lowerIntervalIndex < upperIntervalIndex)
            {
                int middleIntervalIndex = lowerIntervalIndex +
                    ((upperIntervalIndex - lowerIntervalIndex) / 2);
                if (intervals[middleIntervalIndex].MinimumInclusiveKelvin <=
                    integerKelvin)
                {
                    lowerIntervalIndex = middleIntervalIndex + 1;
                }
                else
                {
                    upperIntervalIndex = middleIntervalIndex;
                }
            }

            int candidateIntervalIndex = lowerIntervalIndex - 1;
            return candidateIntervalIndex >= 0 &&
                integerKelvin <
                    intervals[candidateIntervalIndex].MaximumExclusiveKelvin;
        }

        private enum AllowedTemperatureIntervalSetState
        {
            AllowsNoTemperature,
            AllowsEveryTemperature,
            FiniteIntervals
        }

        private sealed class AllowedTemperatureIntervalComparer :
            IComparer<AllowedTemperatureInterval>
        {
            internal static readonly AllowedTemperatureIntervalComparer Instance =
                new AllowedTemperatureIntervalComparer();

            private AllowedTemperatureIntervalComparer()
            {
            }

            public int Compare(
                AllowedTemperatureInterval left,
                AllowedTemperatureInterval right)
            {
                int minimumComparison = left.MinimumInclusiveKelvin.CompareTo(
                    right.MinimumInclusiveKelvin);
                return minimumComparison != 0
                    ? minimumComparison
                    : left.MaximumExclusiveKelvin.CompareTo(
                        right.MaximumExclusiveKelvin);
            }
        }
    }
}
