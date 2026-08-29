#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Owns normalized constraints by component identity and eagerly publishes the
    /// minimal immutable state required by lock-free hot-path readers.
    /// </summary>
    internal sealed class TemperatureConstraintRegistry
    {
        private const int EndpointReferenceCountLength =
            OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1;
        private const int EndpointMembershipWordBitCount = 64;
        private const int EndpointMembershipWordCount =
            (EndpointReferenceCountLength + EndpointMembershipWordBitCount - 1) /
            EndpointMembershipWordBitCount;

        private readonly object registryMutationLock = new object();
        private readonly Dictionary<int, TemperatureConstraintRegistryEntry>
            entriesByComponentInstanceId =
                new Dictionary<int, TemperatureConstraintRegistryEntry>();

        // For current ONI this is 10,001 Int32 elements: 40,004 bytes (about
        // 39.1 KiB) excluding the array header. Extending the former 5000 K range
        // adds about 19.5 KiB once per registry, never once per world/tag/pickup.
        private readonly int[] endpointReferenceCounts =
            new int[EndpointReferenceCountLength];

        // Invariant: an endpoint's membership bit is set exactly when its reference
        // count is positive. Count changes within that positive range do not touch
        // this bitset and therefore do not rebuild the immutable endpoint view.
        private readonly ulong[] activeEndpointMembershipWords =
            new ulong[EndpointMembershipWordCount];

        private int enabledConstraintCount;
        private int enabledNonEmptyConstraintCount;
        private int activeEndpointCount;
        private long nextRegistrationSequence;
        private long generation;
        private ActiveTemperatureConstraintSnapshot publishedSnapshot;

        internal TemperatureConstraintRegistry()
        {
            // Array.AsReadOnly prevents a caller from recovering a mutable int[].
            // Later snapshots reuse this exact view whenever endpoint membership
            // remains unchanged, even when counts or generation change.
            IReadOnlyList<int> emptySortedDecisionEndpointsKelvin =
                Array.AsReadOnly(Array.Empty<int>());
            publishedSnapshot = new ActiveTemperatureConstraintSnapshot(
                new TemperatureConstraintGeneration(0),
                enabledConstraintCount: 0,
                enabledNonEmptyConstraintCount: 0,
                emptySortedDecisionEndpointsKelvin);
        }

        internal ActiveTemperatureConstraintSnapshot CaptureSnapshot() =>
            Volatile.Read(ref publishedSnapshot);

        internal TemperatureConstraintRegistrationToken Register(
            int componentInstanceId,
            DeliveryTemperatureConstraint constraint,
            out bool effectiveStateChanged)
        {
            effectiveStateChanged = false;
            lock (registryMutationLock)
            {
                if (entriesByComponentInstanceId.TryGetValue(
                        componentInstanceId,
                        out var existingEntry) &&
                    existingEntry.Constraint.Equals(constraint))
                {
                    // Exact repeats preserve both ownership and the already-published
                    // snapshot reference; they are genuine O(1) no-ops.
                    return existingEntry.RegistrationToken;
                }

                // Both monotonic values are allocated before any dictionary, count,
                // bitset, or publication mutation. Exhaustion therefore cannot leave
                // a partial replacement or expose a reusable zero/wrapped identity.
                long nextRegistrationSequenceValue =
                    GetNextRegistrationSequenceValue();
                long nextGenerationValue = GetNextGenerationValue();
                var registrationToken = new TemperatureConstraintRegistrationToken(
                    componentInstanceId,
                    nextRegistrationSequenceValue);

                bool endpointMembershipChanged;
                if (existingEntry is null)
                {
                    endpointMembershipChanged = ApplyConstraintTransition(
                        oldConstraintExists: false,
                        oldConstraint: default(DeliveryTemperatureConstraint),
                        newConstraintExists: true,
                        constraint);
                }
                else
                {
                    endpointMembershipChanged = ApplyConstraintTransition(
                        oldConstraintExists: true,
                        existingEntry.Constraint,
                        newConstraintExists: true,
                        constraint);
                }

                entriesByComponentInstanceId[componentInstanceId] =
                    new TemperatureConstraintRegistryEntry(
                        registrationToken,
                        constraint);
                nextRegistrationSequence = nextRegistrationSequenceValue;
                PublishChangedSnapshot(
                    nextGenerationValue,
                    endpointMembershipChanged);
                effectiveStateChanged = true;
                return registrationToken;
            }
        }

        internal bool TryReplace(
            TemperatureConstraintRegistrationToken registrationToken,
            DeliveryTemperatureConstraint constraint,
            out bool effectiveStateChanged)
        {
            effectiveStateChanged = false;
            lock (registryMutationLock)
            {
                if (!TryGetOwnedEntry(registrationToken, out var existingEntry))
                {
                    return false;
                }

                if (existingEntry.Constraint.Equals(constraint))
                {
                    return true;
                }

                long nextGenerationValue = GetNextGenerationValue();
                bool endpointMembershipChanged = ApplyConstraintTransition(
                    oldConstraintExists: true,
                    existingEntry.Constraint,
                    newConstraintExists: true,
                    constraint);
                entriesByComponentInstanceId[registrationToken.ComponentInstanceId] =
                    new TemperatureConstraintRegistryEntry(
                        registrationToken,
                        constraint);
                PublishChangedSnapshot(
                    nextGenerationValue,
                    endpointMembershipChanged);
                effectiveStateChanged = true;
                return true;
            }
        }

        internal bool TryRemove(
            TemperatureConstraintRegistrationToken registrationToken,
            out bool effectiveStateChanged)
        {
            effectiveStateChanged = false;
            lock (registryMutationLock)
            {
                if (!TryGetOwnedEntry(registrationToken, out var existingEntry))
                {
                    // Unknown and stale tokens are deliberately idempotent. In
                    // particular, delayed cleanup cannot remove a replacement.
                    return false;
                }

                long nextGenerationValue = GetNextGenerationValue();
                bool endpointMembershipChanged = ApplyConstraintTransition(
                    oldConstraintExists: true,
                    existingEntry.Constraint,
                    newConstraintExists: false,
                    newConstraint: default(DeliveryTemperatureConstraint));
                entriesByComponentInstanceId.Remove(
                    registrationToken.ComponentInstanceId);
                PublishChangedSnapshot(
                    nextGenerationValue,
                    endpointMembershipChanged);
                effectiveStateChanged = true;
                return true;
            }
        }

        private bool TryGetOwnedEntry(
            TemperatureConstraintRegistrationToken registrationToken,
            out TemperatureConstraintRegistryEntry entry)
        {
            if (entriesByComponentInstanceId.TryGetValue(
                    registrationToken.ComponentInstanceId,
                    out var candidateEntry) &&
                candidateEntry.RegistrationToken.Equals(registrationToken))
            {
                entry = candidateEntry;
                return true;
            }

            entry = null!;
            return false;
        }

        private bool ApplyConstraintTransition(
            bool oldConstraintExists,
            DeliveryTemperatureConstraint oldConstraint,
            bool newConstraintExists,
            DeliveryTemperatureConstraint newConstraint)
        {
            bool oldConstraintIsEnabled =
                oldConstraintExists && oldConstraint.IsEnabled;
            bool newConstraintIsEnabled =
                newConstraintExists && newConstraint.IsEnabled;
            bool oldConstraintContributesEndpoints =
                oldConstraintIsEnabled && !oldConstraint.IsEmpty;
            bool newConstraintContributesEndpoints =
                newConstraintIsEnabled && !newConstraint.IsEmpty;

            int nextEnabledConstraintCount = AddCountDeltaChecked(
                enabledConstraintCount,
                (newConstraintIsEnabled ? 1 : 0) -
                (oldConstraintIsEnabled ? 1 : 0),
                "enabled constraint count");
            int nextEnabledNonEmptyConstraintCount = AddCountDeltaChecked(
                enabledNonEmptyConstraintCount,
                (newConstraintContributesEndpoints ? 1 : 0) -
                (oldConstraintContributesEndpoints ? 1 : 0),
                "enabled nonempty constraint count");

            // Shared old/new endpoints have a net delta of zero. Skipping them is
            // essential: a replacement must not clear and then reset a membership
            // bit, which would trigger an unnecessary 157-word snapshot rebuild.
            ValidateConstraintEndpointAdjustments(
                oldConstraint,
                oldConstraintContributesEndpoints,
                newConstraint,
                newConstraintContributesEndpoints);

            bool endpointMembershipChanged = false;
            ApplyConstraintEndpointAdjustments(
                oldConstraint,
                oldConstraintContributesEndpoints,
                newConstraint,
                newConstraintContributesEndpoints,
                ref endpointMembershipChanged);
            enabledConstraintCount = nextEnabledConstraintCount;
            enabledNonEmptyConstraintCount =
                nextEnabledNonEmptyConstraintCount;
            return endpointMembershipChanged;
        }

        private void ValidateConstraintEndpointAdjustments(
            DeliveryTemperatureConstraint oldConstraint,
            bool oldConstraintContributesEndpoints,
            DeliveryTemperatureConstraint newConstraint,
            bool newConstraintContributesEndpoints)
        {
            if (oldConstraintContributesEndpoints)
            {
                ValidateRemovedEndpointUnlessShared(
                    oldConstraint.MinimumInclusiveKelvin,
                    newConstraint,
                    newConstraintContributesEndpoints);
                ValidateRemovedEndpointUnlessShared(
                    oldConstraint.MaximumExclusiveKelvin,
                    newConstraint,
                    newConstraintContributesEndpoints);
            }

            if (newConstraintContributesEndpoints)
            {
                ValidateAddedEndpointUnlessShared(
                    newConstraint.MinimumInclusiveKelvin,
                    oldConstraint,
                    oldConstraintContributesEndpoints);
                ValidateAddedEndpointUnlessShared(
                    newConstraint.MaximumExclusiveKelvin,
                    oldConstraint,
                    oldConstraintContributesEndpoints);
            }
        }

        private void ApplyConstraintEndpointAdjustments(
            DeliveryTemperatureConstraint oldConstraint,
            bool oldConstraintContributesEndpoints,
            DeliveryTemperatureConstraint newConstraint,
            bool newConstraintContributesEndpoints,
            ref bool endpointMembershipChanged)
        {
            if (oldConstraintContributesEndpoints)
            {
                RemoveEndpointUnlessShared(
                    oldConstraint.MinimumInclusiveKelvin,
                    newConstraint,
                    newConstraintContributesEndpoints,
                    ref endpointMembershipChanged);
                RemoveEndpointUnlessShared(
                    oldConstraint.MaximumExclusiveKelvin,
                    newConstraint,
                    newConstraintContributesEndpoints,
                    ref endpointMembershipChanged);
            }

            if (newConstraintContributesEndpoints)
            {
                AddEndpointUnlessShared(
                    newConstraint.MinimumInclusiveKelvin,
                    oldConstraint,
                    oldConstraintContributesEndpoints,
                    ref endpointMembershipChanged);
                AddEndpointUnlessShared(
                    newConstraint.MaximumExclusiveKelvin,
                    oldConstraint,
                    oldConstraintContributesEndpoints,
                    ref endpointMembershipChanged);
            }
        }

        private void ValidateRemovedEndpointUnlessShared(
            int endpointKelvin,
            DeliveryTemperatureConstraint otherConstraint,
            bool otherConstraintContributesEndpoints)
        {
            if (!ConstraintContainsEndpoint(
                    otherConstraint,
                    otherConstraintContributesEndpoints,
                    endpointKelvin))
            {
                ValidateEndpointReferenceCountAdjustment(endpointKelvin, -1);
            }
        }

        private void ValidateAddedEndpointUnlessShared(
            int endpointKelvin,
            DeliveryTemperatureConstraint otherConstraint,
            bool otherConstraintContributesEndpoints)
        {
            if (!ConstraintContainsEndpoint(
                    otherConstraint,
                    otherConstraintContributesEndpoints,
                    endpointKelvin))
            {
                ValidateEndpointReferenceCountAdjustment(endpointKelvin, 1);
            }
        }

        private void RemoveEndpointUnlessShared(
            int endpointKelvin,
            DeliveryTemperatureConstraint otherConstraint,
            bool otherConstraintContributesEndpoints,
            ref bool endpointMembershipChanged)
        {
            if (!ConstraintContainsEndpoint(
                    otherConstraint,
                    otherConstraintContributesEndpoints,
                    endpointKelvin))
            {
                AdjustEndpointReferenceCount(
                    endpointKelvin,
                    -1,
                    ref endpointMembershipChanged);
            }
        }

        private void AddEndpointUnlessShared(
            int endpointKelvin,
            DeliveryTemperatureConstraint otherConstraint,
            bool otherConstraintContributesEndpoints,
            ref bool endpointMembershipChanged)
        {
            if (!ConstraintContainsEndpoint(
                    otherConstraint,
                    otherConstraintContributesEndpoints,
                    endpointKelvin))
            {
                AdjustEndpointReferenceCount(
                    endpointKelvin,
                    1,
                    ref endpointMembershipChanged);
            }
        }

        private static bool ConstraintContainsEndpoint(
            DeliveryTemperatureConstraint constraint,
            bool constraintContributesEndpoints,
            int endpointKelvin) =>
            constraintContributesEndpoints &&
            (constraint.MinimumInclusiveKelvin == endpointKelvin ||
             constraint.MaximumExclusiveKelvin == endpointKelvin);

        private void ValidateEndpointReferenceCountAdjustment(
            int endpointKelvin,
            int adjustment)
        {
            int currentReferenceCount = endpointReferenceCounts[endpointKelvin];
            bool membershipBitIsSet = IsEndpointMembershipBitSet(endpointKelvin);
            if ((currentReferenceCount > 0) != membershipBitIsSet)
            {
                throw new InvalidOperationException(
                    $"Temperature endpoint {endpointKelvin} K has inconsistent " +
                    "reference-count and membership-bit state.");
            }

            if (adjustment < 0 && currentReferenceCount <= 0)
            {
                throw new InvalidOperationException(
                    $"Temperature endpoint {endpointKelvin} K reference count " +
                    "cannot be decremented below zero.");
            }

            if (adjustment > 0 && currentReferenceCount == int.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Temperature endpoint {endpointKelvin} K reference count " +
                    "cannot exceed Int32.MaxValue.");
            }
        }

        private void AdjustEndpointReferenceCount(
            int endpointKelvin,
            int adjustment,
            ref bool endpointMembershipChanged)
        {
            int currentReferenceCount = endpointReferenceCounts[endpointKelvin];
            int nextReferenceCount = checked(currentReferenceCount + adjustment);
            endpointReferenceCounts[endpointKelvin] = nextReferenceCount;

            if (currentReferenceCount == 0)
            {
                SetEndpointMembershipBit(endpointKelvin);
                activeEndpointCount = checked(activeEndpointCount + 1);
                endpointMembershipChanged = true;
            }
            else if (nextReferenceCount == 0)
            {
                if (activeEndpointCount <= 0)
                {
                    throw new InvalidOperationException(
                        "Active temperature endpoint count cannot be decremented " +
                        "below zero.");
                }

                ClearEndpointMembershipBit(endpointKelvin);
                activeEndpointCount--;
                endpointMembershipChanged = true;
            }
        }

        private bool IsEndpointMembershipBitSet(int endpointKelvin)
        {
            int wordIndex = endpointKelvin / EndpointMembershipWordBitCount;
            int bitIndex = endpointKelvin % EndpointMembershipWordBitCount;
            ulong bit = 1UL << bitIndex;
            return (activeEndpointMembershipWords[wordIndex] & bit) != 0;
        }

        private void SetEndpointMembershipBit(int endpointKelvin)
        {
            int wordIndex = endpointKelvin / EndpointMembershipWordBitCount;
            int bitIndex = endpointKelvin % EndpointMembershipWordBitCount;
            ulong bit = 1UL << bitIndex;
            if ((activeEndpointMembershipWords[wordIndex] & bit) != 0)
            {
                throw new InvalidOperationException(
                    $"Temperature endpoint {endpointKelvin} K membership bit " +
                    "was already set before its first reference was added.");
            }

            activeEndpointMembershipWords[wordIndex] |= bit;
        }

        private void ClearEndpointMembershipBit(int endpointKelvin)
        {
            int wordIndex = endpointKelvin / EndpointMembershipWordBitCount;
            int bitIndex = endpointKelvin % EndpointMembershipWordBitCount;
            ulong bit = 1UL << bitIndex;
            if ((activeEndpointMembershipWords[wordIndex] & bit) == 0)
            {
                throw new InvalidOperationException(
                    $"Temperature endpoint {endpointKelvin} K membership bit " +
                    "was absent when its last reference was removed.");
            }

            activeEndpointMembershipWords[wordIndex] &= ~bit;
        }

        private void PublishChangedSnapshot(
            long nextGenerationValue,
            bool endpointMembershipChanged)
        {
            IReadOnlyList<int> sortedDecisionEndpointsKelvin =
                publishedSnapshot.SortedDecisionEndpointsKelvin;
            if (endpointMembershipChanged)
            {
                sortedDecisionEndpointsKelvin =
                    CreateSortedDecisionEndpointView();
            }

            var nextSnapshot = new ActiveTemperatureConstraintSnapshot(
                new TemperatureConstraintGeneration(nextGenerationValue),
                enabledConstraintCount,
                enabledNonEmptyConstraintCount,
                sortedDecisionEndpointsKelvin);
            generation = nextGenerationValue;

            // All fields are complete before the single reference publication.
            // Readers use Volatile.Read and can observe only a whole old or whole
            // new immutable state, never independently changing fields.
            Volatile.Write(ref publishedSnapshot, nextSnapshot);
        }

        private IReadOnlyList<int> CreateSortedDecisionEndpointView()
        {
            var sortedDecisionEndpointsKelvin = new int[activeEndpointCount];
            int emittedEndpointCount = 0;

            // This is the only registry range-shaped reconstruction. It scans 157
            // fixed membership words for the current ONI bound, then emits only set
            // endpoints. It never scans registered components or all 10,001 counts.
            // Unused 5000..9999 K values therefore add fixed memory but no recurring
            // mutation work unless one of their membership bits is actually active.
            for (var wordIndex = 0;
                 wordIndex < activeEndpointMembershipWords.Length;
                 wordIndex++)
            {
                ulong remainingMembershipBits =
                    activeEndpointMembershipWords[wordIndex];
                while (remainingMembershipBits != 0)
                {
                    int bitIndex = CountTrailingZeroBits(
                        remainingMembershipBits);
                    int endpointKelvin =
                        (wordIndex * EndpointMembershipWordBitCount) + bitIndex;
                    if (endpointKelvin >= EndpointReferenceCountLength ||
                        emittedEndpointCount >= sortedDecisionEndpointsKelvin.Length)
                    {
                        throw new InvalidOperationException(
                            "Active temperature endpoint membership exceeded its " +
                            "fixed ONI-bound representation.");
                    }

                    sortedDecisionEndpointsKelvin[emittedEndpointCount] =
                        endpointKelvin;
                    emittedEndpointCount++;
                    remainingMembershipBits &= remainingMembershipBits - 1;
                }
            }

            if (emittedEndpointCount != sortedDecisionEndpointsKelvin.Length)
            {
                throw new InvalidOperationException(
                    "Active temperature endpoint count did not match membership " +
                    "bit enumeration.");
            }

            return Array.AsReadOnly(sortedDecisionEndpointsKelvin);
        }

        private static int CountTrailingZeroBits(ulong nonzeroValue)
        {
            if (nonzeroValue == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nonzeroValue));
            }

            // The game-loaded netstandard2.1 compile graph does not expose
            // System.Numerics.BitOperations. This fixed six-branch search keeps the
            // set-bit emission allocation-free and O(1) without adding a package,
            // lookup table, framework shim, or target-dependent implementation.
            int trailingZeroBitCount = 0;
            if ((nonzeroValue & 0xFFFFFFFFUL) == 0)
            {
                trailingZeroBitCount += 32;
                nonzeroValue >>= 32;
            }

            if ((nonzeroValue & 0xFFFFUL) == 0)
            {
                trailingZeroBitCount += 16;
                nonzeroValue >>= 16;
            }

            if ((nonzeroValue & 0xFFUL) == 0)
            {
                trailingZeroBitCount += 8;
                nonzeroValue >>= 8;
            }

            if ((nonzeroValue & 0xFUL) == 0)
            {
                trailingZeroBitCount += 4;
                nonzeroValue >>= 4;
            }

            if ((nonzeroValue & 0x3UL) == 0)
            {
                trailingZeroBitCount += 2;
                nonzeroValue >>= 2;
            }

            if ((nonzeroValue & 0x1UL) == 0)
            {
                trailingZeroBitCount++;
            }

            return trailingZeroBitCount;
        }

        private long GetNextRegistrationSequenceValue()
        {
            try
            {
                long nextValue = checked(nextRegistrationSequence + 1);
                if (nextValue == 0)
                {
                    throw new InvalidOperationException(
                        "Temperature constraint registration sequence cannot use zero.");
                }

                return nextValue;
            }
            catch (OverflowException exception)
            {
                throw new InvalidOperationException(
                    "Temperature constraint registration sequence is exhausted; " +
                    "the registry will not wrap or reuse an ownership identity.",
                    exception);
            }
        }

        private long GetNextGenerationValue()
        {
            try
            {
                long nextValue = checked(generation + 1);
                if (nextValue == 0)
                {
                    throw new InvalidOperationException(
                        "Temperature constraint generation cannot use zero after " +
                        "the initial empty snapshot.");
                }

                return nextValue;
            }
            catch (OverflowException exception)
            {
                throw new InvalidOperationException(
                    "Temperature constraint generation is exhausted; the registry " +
                    "will not publish a wrapped or reusable generation.",
                    exception);
            }
        }

        private static int AddCountDeltaChecked(
            int currentCount,
            int delta,
            string countIdentity)
        {
            try
            {
                int nextCount = checked(currentCount + delta);
                if (nextCount < 0)
                {
                    throw new InvalidOperationException(
                        $"Temperature constraint registry {countIdentity} cannot " +
                        "become negative.");
                }

                return nextCount;
            }
            catch (OverflowException exception)
            {
                throw new InvalidOperationException(
                    $"Temperature constraint registry {countIdentity} exceeded " +
                    "Int32 capacity.",
                    exception);
            }
        }

        private sealed class TemperatureConstraintRegistryEntry
        {
            internal TemperatureConstraintRegistryEntry(
                TemperatureConstraintRegistrationToken registrationToken,
                DeliveryTemperatureConstraint constraint)
            {
                RegistrationToken = registrationToken;
                Constraint = constraint;
            }

            internal TemperatureConstraintRegistrationToken RegistrationToken { get; }

            internal DeliveryTemperatureConstraint Constraint { get; }
        }
    }
}
