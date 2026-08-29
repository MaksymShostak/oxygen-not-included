#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Reusable scratch storage for accumulating one resource tag's amount by
    /// behaviorally distinct temperature bucket.
    /// </summary>
    /// <remarks>
    /// A generation stamp makes beginning an ordinary resource tag O(1): stale
    /// values remain in the fixed buffers but are ignored until first touched in
    /// the current generation. The only complete-buffer reset occurs when the
    /// positive stamp space is exhausted, after roughly two billion generations.
    /// </remarks>
    internal sealed class TemperatureAmountAccumulator
    {
        private readonly float[] amountsByBucket = new float[TemperatureDecisionBucket.BucketCount];
        private readonly int[] stampsByBucket = new int[TemperatureDecisionBucket.BucketCount];
        private readonly int[] touchedBucketOrdinals = new int[TemperatureDecisionBucket.BucketCount];

        private int stamp;
        private int touchedBucketCount;
        private bool resourceTagIsOpen;

        /// <summary>
        /// Starts accumulation for exactly one resource tag without clearing the
        /// complete canonical bucket range during ordinary operation.
        /// </summary>
        internal void BeginResourceTag()
        {
            if (resourceTagIsOpen)
            {
                throw new InvalidOperationException(
                    "The accumulator is already collecting a resource tag. " +
                    "Build that series before beginning another resource tag.");
            }

            if (stamp == int.MaxValue)
            {
                // Stamp zero means "never touched". Returning to stamp one after
                // clearing both stamped buffers prevents any historical entry
                // from being mistaken for data in the new generation.
                Array.Clear(amountsByBucket, 0, amountsByBucket.Length);
                Array.Clear(stampsByBucket, 0, stampsByBucket.Length);
                stamp = 1;
            }
            else
            {
                stamp++;
            }

            // The ordinal array may retain old integers. Only this prefix belongs
            // to the current generation, so resetting its logical length is O(1).
            touchedBucketCount = 0;
            resourceTagIsOpen = true;
        }

        /// <summary>
        /// Adds an amount to the canonical decision bucket for one observed item.
        /// </summary>
        internal void AddTemperatureAmount(
            float temperatureKelvin,
            float amount)
        {
            ThrowIfResourceTagIsNotOpen();

            // Exact zero contributes nothing and must not make a bucket appear
            // occupied in the sparse publication.
            if (amount == 0.0f)
            {
                return;
            }

            int bucketOrdinal =
                TemperatureDecisionBucket.FromTemperature(temperatureKelvin).Ordinal;
            if (stampsByBucket[bucketOrdinal] != stamp)
            {
                // First touch in this generation overwrites any stale amount.
                // Recording the ordinal once bounds all later work by the number
                // of occupied candidates rather than the 10,002-bucket universe.
                stampsByBucket[bucketOrdinal] = stamp;
                amountsByBucket[bucketOrdinal] = amount;
                touchedBucketOrdinals[touchedBucketCount] = bucketOrdinal;
                touchedBucketCount++;
                return;
            }

            amountsByBucket[bucketOrdinal] += amount;
        }

        /// <summary>
        /// Publishes an immutable, ordinal-sorted prefix-sum series for the open
        /// resource tag and closes that accumulation lifecycle.
        /// </summary>
        internal TemperatureAmountSeries BuildSeries()
        {
            ThrowIfResourceTagIsNotOpen();

            // Compact only ordinals touched by this tag. Exact cancellations are
            // deliberately omitted so query cost and publication size track the
            // genuinely occupied bucket count.
            int occupiedBucketCount = 0;
            for (int touchedBucketIndex = 0;
                 touchedBucketIndex < touchedBucketCount;
                 touchedBucketIndex++)
            {
                int bucketOrdinal = touchedBucketOrdinals[touchedBucketIndex];
                if (amountsByBucket[bucketOrdinal] != 0.0f)
                {
                    touchedBucketOrdinals[occupiedBucketCount] = bucketOrdinal;
                    occupiedBucketCount++;
                }
            }

            if (occupiedBucketCount == 0)
            {
                resourceTagIsOpen = false;
                return TemperatureAmountSeries.Empty;
            }

            // Sorting the compact prefix—not the entire canonical range—keeps
            // work proportional to the temperatures actually observed.
            Array.Sort(touchedBucketOrdinals, 0, occupiedBucketCount);

            var occupiedBucketOrdinals = new int[occupiedBucketCount];
            var cumulativeAmounts = new float[occupiedBucketCount];
            float cumulativeAmount = 0.0f;
            for (int occupiedBucketIndex = 0;
                 occupiedBucketIndex < occupiedBucketCount;
                 occupiedBucketIndex++)
            {
                int bucketOrdinal = touchedBucketOrdinals[occupiedBucketIndex];
                occupiedBucketOrdinals[occupiedBucketIndex] = bucketOrdinal;
                cumulativeAmount += amountsByBucket[bucketOrdinal];
                cumulativeAmounts[occupiedBucketIndex] = cumulativeAmount;
            }

            // Ownership of both freshly allocated arrays transfers to the
            // publication. The reusable buffers are never exposed or retained.
            TemperatureAmountSeries series =
                TemperatureAmountSeries.CreateFromOwnedArrays(
                    occupiedBucketOrdinals,
                    cumulativeAmounts);
            resourceTagIsOpen = false;
            return series;
        }

        private void ThrowIfResourceTagIsNotOpen()
        {
            if (!resourceTagIsOpen)
            {
                throw new InvalidOperationException(
                    "BeginResourceTag must be called before adding amounts or " +
                    "building a temperature amount series.");
            }
        }
    }
}
