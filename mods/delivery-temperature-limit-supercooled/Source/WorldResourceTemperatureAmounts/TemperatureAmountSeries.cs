#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Immutable sparse prefix sums for one resource tag's amounts by canonical
    /// temperature decision bucket.
    /// </summary>
    /// <remarks>
    /// Queries perform two binary searches over occupied buckets and one prefix
    /// subtraction. Consequently, unused portions of ONI's storable-temperature
    /// range impose no recurring query cost.
    /// </remarks>
    internal sealed class TemperatureAmountSeries
    {
        internal static readonly TemperatureAmountSeries Empty =
            new TemperatureAmountSeries(Array.Empty<int>(), Array.Empty<float>());

        private readonly int[] occupiedBucketOrdinals;
        private readonly float[] cumulativeAmounts;

        private TemperatureAmountSeries(
            int[] occupiedBucketOrdinals,
            float[] cumulativeAmounts)
        {
            this.occupiedBucketOrdinals = occupiedBucketOrdinals;
            this.cumulativeAmounts = cumulativeAmounts;
        }

        internal int OccupiedBucketCount => occupiedBucketOrdinals.Length;

        internal float TotalAmount =>
            cumulativeAmounts.Length == 0
                ? 0.0f
                : cumulativeAmounts[cumulativeAmounts.Length - 1];

        /// <summary>
        /// Accepts ownership of publication-only arrays after validating the
        /// invariants on which binary search and prefix subtraction depend.
        /// </summary>
        internal static TemperatureAmountSeries CreateFromOwnedArrays(
            int[] occupiedBucketOrdinals,
            float[] cumulativeAmounts)
        {
            if (occupiedBucketOrdinals == null)
            {
                throw new ArgumentNullException(nameof(occupiedBucketOrdinals));
            }

            if (cumulativeAmounts == null)
            {
                throw new ArgumentNullException(nameof(cumulativeAmounts));
            }

            if (occupiedBucketOrdinals.Length != cumulativeAmounts.Length)
            {
                throw new ArgumentException(
                    "Occupied bucket ordinals and cumulative amounts must have " +
                    "the same length.",
                    nameof(cumulativeAmounts));
            }

            int previousBucketOrdinal = -1;
            for (int occupiedBucketIndex = 0;
                 occupiedBucketIndex < occupiedBucketOrdinals.Length;
                 occupiedBucketIndex++)
            {
                int bucketOrdinal = occupiedBucketOrdinals[occupiedBucketIndex];
                if (bucketOrdinal < 0 ||
                    bucketOrdinal >= TemperatureDecisionBucket.BucketCount)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(occupiedBucketOrdinals),
                        bucketOrdinal,
                        "Every occupied bucket ordinal must be canonical.");
                }

                if (bucketOrdinal <= previousBucketOrdinal)
                {
                    throw new ArgumentException(
                        "Occupied bucket ordinals must be strictly increasing.",
                        nameof(occupiedBucketOrdinals));
                }

                previousBucketOrdinal = bucketOrdinal;
            }

            if (occupiedBucketOrdinals.Length == 0)
            {
                return Empty;
            }

            // The caller transfers arrays that were allocated solely for this
            // publication. Neither this type nor its caller mutates them again.
            return new TemperatureAmountSeries(
                occupiedBucketOrdinals,
                cumulativeAmounts);
        }

        /// <summary>
        /// Combines one immutable sparse contribution per listed member world.
        /// Repeated references are deliberately repeated contributions, not values
        /// to deduplicate.
        /// </summary>
        internal static TemperatureAmountSeries Combine(
            System.Collections.Generic.IReadOnlyList<TemperatureAmountSeries>
                sourceSeries)
        {
            if (sourceSeries == null)
            {
                throw new ArgumentNullException(nameof(sourceSeries));
            }

            int contributionBucketCount = 0;
            for (int sourceSeriesIndex = 0;
                 sourceSeriesIndex < sourceSeries.Count;
                 sourceSeriesIndex++)
            {
                TemperatureAmountSeries? source =
                    sourceSeries[sourceSeriesIndex];
                if (source == null)
                {
                    throw new ArgumentNullException(
                        nameof(sourceSeries),
                        "A source temperature amount series cannot be null.");
                }

                contributionBucketCount = checked(
                    contributionBucketCount + source.OccupiedBucketCount);
            }

            if (contributionBucketCount == 0)
            {
                return Empty;
            }

            // Flatten only occupied contributions. Sorting these sparse entries
            // avoids allocating or scanning the complete canonical bucket range.
            var contributionBucketOrdinals =
                new int[contributionBucketCount];
            var contributionAmounts = new float[contributionBucketCount];
            int contributionIndex = 0;
            for (int sourceSeriesIndex = 0;
                 sourceSeriesIndex < sourceSeries.Count;
                 sourceSeriesIndex++)
            {
                TemperatureAmountSeries source = sourceSeries[sourceSeriesIndex];
                float previousCumulativeAmount = 0.0f;
                for (int sourceBucketIndex = 0;
                     sourceBucketIndex < source.occupiedBucketOrdinals.Length;
                     sourceBucketIndex++)
                {
                    float sourceCumulativeAmount =
                        source.cumulativeAmounts[sourceBucketIndex];
                    contributionBucketOrdinals[contributionIndex] =
                        source.occupiedBucketOrdinals[sourceBucketIndex];
                    contributionAmounts[contributionIndex] =
                        sourceCumulativeAmount - previousCumulativeAmount;
                    previousCumulativeAmount = sourceCumulativeAmount;
                    contributionIndex++;
                }
            }

            Array.Sort(
                contributionBucketOrdinals,
                contributionAmounts,
                0,
                contributionBucketCount);

            var combinedBucketOrdinals = new int[contributionBucketCount];
            var combinedCumulativeAmounts = new float[contributionBucketCount];
            int combinedBucketCount = 0;
            float combinedCumulativeAmount = 0.0f;
            int nextContributionIndex = 0;
            while (nextContributionIndex < contributionBucketCount)
            {
                int bucketOrdinal =
                    contributionBucketOrdinals[nextContributionIndex];
                float bucketAmount = 0.0f;
                do
                {
                    bucketAmount += contributionAmounts[nextContributionIndex];
                    nextContributionIndex++;
                }
                while (nextContributionIndex < contributionBucketCount &&
                    contributionBucketOrdinals[nextContributionIndex] ==
                        bucketOrdinal);

                // Cross-world contributions may cancel exactly. Omitting that
                // bucket preserves the sparse representation contract.
                if (bucketAmount != 0.0f)
                {
                    combinedBucketOrdinals[combinedBucketCount] = bucketOrdinal;
                    combinedCumulativeAmount += bucketAmount;
                    combinedCumulativeAmounts[combinedBucketCount] =
                        combinedCumulativeAmount;
                    combinedBucketCount++;
                }
            }

            if (combinedBucketCount == 0)
            {
                return Empty;
            }

            if (combinedBucketCount != contributionBucketCount)
            {
                var exactCombinedBucketOrdinals =
                    new int[combinedBucketCount];
                var exactCombinedCumulativeAmounts =
                    new float[combinedBucketCount];
                Array.Copy(
                    combinedBucketOrdinals,
                    exactCombinedBucketOrdinals,
                    combinedBucketCount);
                Array.Copy(
                    combinedCumulativeAmounts,
                    exactCombinedCumulativeAmounts,
                    combinedBucketCount);
                combinedBucketOrdinals = exactCombinedBucketOrdinals;
                combinedCumulativeAmounts = exactCombinedCumulativeAmounts;
            }

            return CreateFromOwnedArrays(
                combinedBucketOrdinals,
                combinedCumulativeAmounts);
        }

        /// <summary>
        /// Returns the amount admitted by a normalized inclusive-minimum,
        /// exclusive-maximum delivery constraint.
        /// </summary>
        internal float GetAmountAllowedBy(
            DeliveryTemperatureConstraint constraint)
        {
            if (occupiedBucketOrdinals.Length == 0 || constraint.IsEmpty)
            {
                return 0.0f;
            }

            if (!constraint.IsEnabled)
            {
                // Disabled filtering preserves all observed amounts, including
                // the below-range and at-or-above-maximum sentinel buckets.
                return TotalAmount;
            }

            // Ordinary integer-Kelvin bucket k has ordinal First + k. Mapping
            // both constraint boundaries this way automatically excludes the
            // below-range sentinel at zero and, for a 10,000 K maximum, the
            // at-or-above-maximum sentinel at ordinal 10,001.
            int minimumInclusiveBucketOrdinal =
                TemperatureDecisionBucket.FirstIntegerKelvinOrdinal +
                constraint.MinimumInclusiveKelvin;
            int maximumExclusiveBucketOrdinal =
                TemperatureDecisionBucket.FirstIntegerKelvinOrdinal +
                constraint.MaximumExclusiveKelvin;

            int firstAllowedBucketIndex = LowerBound(
                occupiedBucketOrdinals,
                minimumInclusiveBucketOrdinal);
            int firstDisallowedBucketIndex = LowerBound(
                occupiedBucketOrdinals,
                maximumExclusiveBucketOrdinal);

            float amountBeforeAllowedRange = firstAllowedBucketIndex == 0
                ? 0.0f
                : cumulativeAmounts[firstAllowedBucketIndex - 1];
            float amountThroughAllowedRange = firstDisallowedBucketIndex == 0
                ? 0.0f
                : cumulativeAmounts[firstDisallowedBucketIndex - 1];
            return amountThroughAllowedRange - amountBeforeAllowedRange;
        }

        private static int LowerBound(
            int[] sortedBucketOrdinals,
            int soughtBucketOrdinal)
        {
            int lowerIndex = 0;
            int upperIndex = sortedBucketOrdinals.Length;
            while (lowerIndex < upperIndex)
            {
                int middleIndex = lowerIndex + ((upperIndex - lowerIndex) / 2);
                if (sortedBucketOrdinals[middleIndex] < soughtBucketOrdinal)
                {
                    lowerIndex = middleIndex + 1;
                }
                else
                {
                    upperIndex = middleIndex;
                }
            }

            return lowerIndex;
        }
    }
}
