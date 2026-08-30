#nullable enable

using System;
using System.Collections.Generic;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Allocates collision-free FastTrack integer keys within one pickup update.
    /// </summary>
    internal sealed class FastTrackPickupGroupingKeyAllocator
    {
        private Dictionary<FastTrackPickupGroupingCompositeIdentity, int>
            allocatedGroupingKeysByCompositeIdentity =
                new Dictionary<FastTrackPickupGroupingCompositeIdentity, int>();

        private int nextAllocatedGroupingKey;
        private bool temperatureGroupingIsActive;
        private bool isUpdateActive;

        internal void Begin(bool temperatureGroupingIsActive)
        {
            if (isUpdateActive)
            {
                throw new InvalidOperationException(
                    "A FastTrack pickup grouping-key allocation update is already " +
                    "active.");
            }

            this.temperatureGroupingIsActive = temperatureGroupingIsActive;
            nextAllocatedGroupingKey = 0;
            isUpdateActive = true;
        }

        internal int GetOrAllocate(
            int originalTagBitsHash,
            TemperatureEligibilityClassKey temperatureEligibilityClass)
        {
            if (!isUpdateActive)
            {
                throw new InvalidOperationException(
                    "Begin must start a FastTrack pickup grouping-key allocation " +
                    "update before a key can be requested.");
            }

            if (!temperatureGroupingIsActive)
            {
                // When no temperature distinction is required, preserve FastTrack's
                // exact original key and retain no composite mapping state.
                return originalTagBitsHash;
            }

            var pickupGroupingIdentity =
                new FastTrackPickupGroupingCompositeIdentity(
                    originalTagBitsHash,
                    temperatureEligibilityClass);
            if (allocatedGroupingKeysByCompositeIdentity.TryGetValue(
                    pickupGroupingIdentity,
                    out var existingGroupingKey))
            {
                return existingGroupingKey;
            }

            if (nextAllocatedGroupingKey == int.MaxValue)
            {
                throw CreateGroupingKeySpaceExhaustedException();
            }

            int allocatedGroupingKey = nextAllocatedGroupingKey;
            int subsequentGroupingKey;
            try
            {
                subsequentGroupingKey = checked(nextAllocatedGroupingKey + 1);
            }
            catch (OverflowException exception)
            {
                throw new InvalidOperationException(
                    "The FastTrack pickup grouping-key allocation space is " +
                    "exhausted; integer keys will not wrap or be reused.",
                    exception);
            }

            // The dictionary's hash code is only an ordinary lookup accelerator;
            // the externally returned key is this sequential allocation. Therefore
            // even adversarial collisions in originalTagBitsHash or the composite's
            // dictionary hash cannot merge two distinct semantic identities.
            allocatedGroupingKeysByCompositeIdentity.Add(
                pickupGroupingIdentity,
                allocatedGroupingKey);
            nextAllocatedGroupingKey = subsequentGroupingKey;
            return allocatedGroupingKey;
        }

        internal void Complete() => CompleteOrDiscard();

        internal void Discard() => CompleteOrDiscard();

        private void CompleteOrDiscard()
        {
            if (!isUpdateActive)
            {
                return;
            }

            int priorPickupGroupingIdentityCount =
                allocatedGroupingKeysByCompositeIdentity.Count;
            isUpdateActive = false;
            temperatureGroupingIsActive = false;
            nextAllocatedGroupingKey = 0;

            if (priorPickupGroupingIdentityCount >
                RetainedCollectionCapacityLimits
                    .MaximumRetainedFastTrackGroupingKeyCount)
            {
                // Process every composite first, then release only oversized
                // reusable backing storage at the explicit update boundary.
                allocatedGroupingKeysByCompositeIdentity =
                    new Dictionary<
                        FastTrackPickupGroupingCompositeIdentity,
                        int>();
                return;
            }

            allocatedGroupingKeysByCompositeIdentity.Clear();
        }

        private static InvalidOperationException
            CreateGroupingKeySpaceExhaustedException() =>
            new InvalidOperationException(
                "The FastTrack pickup grouping-key allocation space is exhausted; " +
                "integer keys will not wrap or be reused.");

        private readonly struct FastTrackPickupGroupingCompositeIdentity :
            IEquatable<FastTrackPickupGroupingCompositeIdentity>
        {
            internal FastTrackPickupGroupingCompositeIdentity(
                int originalTagBitsHash,
                TemperatureEligibilityClassKey temperatureEligibilityClass)
            {
                OriginalTagBitsHash = originalTagBitsHash;
                TemperatureEligibilityClass = temperatureEligibilityClass;
            }

            internal int OriginalTagBitsHash { get; }

            internal TemperatureEligibilityClassKey TemperatureEligibilityClass
            {
                get;
            }

            public bool Equals(
                FastTrackPickupGroupingCompositeIdentity other) =>
                OriginalTagBitsHash == other.OriginalTagBitsHash &&
                TemperatureEligibilityClass.Equals(
                    other.TemperatureEligibilityClass);

            public override bool Equals(object? obj) =>
                obj is FastTrackPickupGroupingCompositeIdentity other &&
                Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (OriginalTagBitsHash * 397) ^
                        TemperatureEligibilityClass.GetHashCode();
                }
            }
        }
    }
}
