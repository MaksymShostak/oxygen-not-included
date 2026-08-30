#nullable enable

using System;
using System.Collections.Generic;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Owns stable temperature classification state for one pickup-grouping update.
    /// </summary>
    internal sealed class PickupTemperatureGroupingSession
    {
        private Dictionary<int, TemperatureEligibilityClassKey>
            temperatureEligibilityClassByPickupInstanceId =
                new Dictionary<int, TemperatureEligibilityClassKey>();
        private Dictionary<
            PickupTagIdentity,
            ApplicableRequestedTagPartitionResolution>
                firstApplicableRequestedTagPartitionResolutionByPickupTagIdentity =
                    new Dictionary<
                        PickupTagIdentity,
                        ApplicableRequestedTagPartitionResolution>();
        private Dictionary<
            SortedDecisionEndpointSequenceKey,
            TemperaturePartitionDefinition>
                temperaturePartitionDefinitionByDecisionEndpoints =
                    new Dictionary<
                        SortedDecisionEndpointSequenceKey,
                        TemperaturePartitionDefinition>();

        private DeliveryTemperatureGameSession? capturedGameSession;
        private ActiveTemperatureConstraintSnapshot?
            capturedActiveTemperatureConstraints;
        private FetchTemperatureEligibilitySnapshot?
            capturedEligibilitySnapshot;
        private WorldParentTopologySnapshot? capturedWorldTopology;
        private int? resolvedParentWorldId;
        private TemperatureClassificationMode classificationMode;
        private int lastAssignedPartitionDefinitionId;
        private bool isActive;

        internal void Begin(
            DeliveryTemperatureGameSession session,
            int? resolvedParentWorldId,
            ActiveTemperatureConstraintSnapshot constraints,
            FetchTemperatureEligibilitySnapshot? eligibilitySnapshot,
            WorldParentTopologySnapshot worldTopology)
        {
            if (isActive)
            {
                throw new InvalidOperationException(
                    "A pickup temperature grouping update is already active.");
            }

            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (constraints == null)
            {
                throw new ArgumentNullException(nameof(constraints));
            }

            if (worldTopology == null)
            {
                throw new ArgumentNullException(nameof(worldTopology));
            }

            if (resolvedParentWorldId.HasValue &&
                resolvedParentWorldId.Value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(resolvedParentWorldId),
                    resolvedParentWorldId.Value,
                    "A resolved ONI parent-world identity cannot be negative. " +
                    "Use null when the parent world cannot be resolved.");
            }

            // Re-read each session-owned publication only here, at the update
            // boundary. Classify never observes a newer generation midway through
            // sorting or duplicate suppression, so every candidate in this update
            // uses one coherent semantic key space.
            ActiveTemperatureConstraintSnapshot currentConstraints =
                session.TemperatureConstraints.CaptureSnapshot();
            WorldParentTopologySnapshot currentWorldTopology =
                session.WorldParentTopology.CaptureSnapshot();
            FetchRequestTopologyVersion currentFetchTopologyVersion =
                session.FetchRequestTopology.CaptureVersion();
            bool capturedConstraintsAreCurrent =
                constraints.Generation.Equals(currentConstraints.Generation);

            TemperatureClassificationMode selectedMode;
            if (capturedConstraintsAreCurrent &&
                constraints.EnabledConstraintCount == 0)
            {
                selectedMode =
                    TemperatureClassificationMode.NoTemperatureDistinction;
            }
            else if (capturedConstraintsAreCurrent &&
                     constraints.EnabledConstraintCount > 0 &&
                     resolvedParentWorldId.HasValue &&
                     eligibilitySnapshot != null &&
                     session.IsAcceptingPublications &&
                     worldTopology.GameSessionGeneration.Equals(
                         session.Generation) &&
                     worldTopology.Version.Equals(
                         currentWorldTopology.Version) &&
                     eligibilitySnapshot.GameSessionGeneration.Equals(
                         session.Generation) &&
                     eligibilitySnapshot.ConstraintGeneration.Equals(
                         constraints.Generation) &&
                     eligibilitySnapshot.FetchTopologyVersion.Equals(
                         currentFetchTopologyVersion) &&
                     eligibilitySnapshot.WorldTopologyVersion.Equals(
                         worldTopology.Version))
            {
                selectedMode =
                    TemperatureClassificationMode.CurrentScopedSnapshot;
            }
            else
            {
                // Exact buckets are deliberately more fragmented than an optimized
                // partition, but they cannot merge temperatures that any valid
                // enabled constraint might distinguish. Missing or stale evidence
                // therefore sacrifices only optimization, never correctness.
                selectedMode = TemperatureClassificationMode.ExactDecisionFallback;
            }

            capturedGameSession = session;
            capturedActiveTemperatureConstraints = constraints;
            capturedEligibilitySnapshot = eligibilitySnapshot;
            capturedWorldTopology = worldTopology;
            this.resolvedParentWorldId = resolvedParentWorldId;
            classificationMode = selectedMode;
            lastAssignedPartitionDefinitionId = 0;
            isActive = true;
        }

        internal TemperatureEligibilityClassKey Classify(
            int pickupInstanceId,
            PickupTagIdentity tagIdentity,
            IReadOnlyList<Tag> applicableRequestedTags,
            bool hasPrimaryElement,
            float temperatureKelvin)
        {
            if (!isActive)
            {
                throw new InvalidOperationException(
                    "Begin must start a pickup temperature grouping update before " +
                    "a pickup can be classified.");
            }

            if (applicableRequestedTags == null)
            {
                throw new ArgumentNullException(nameof(applicableRequestedTags));
            }

            if (classificationMode ==
                TemperatureClassificationMode.NoTemperatureDistinction)
            {
                // Preserve ONI's original grouping shape and allocate/cache nothing
                // while the feature is bypassed.
                return TemperatureEligibilityClassKey.NoTemperatureDistinction();
            }

            if (temperatureEligibilityClassByPickupInstanceId.TryGetValue(
                    pickupInstanceId,
                    out var cachedClassification))
            {
                return cachedClassification;
            }

            TemperatureEligibilityClassKey classification;
            if (!hasPrimaryElement)
            {
                classification =
                    TemperatureEligibilityClassKey.MissingPrimaryElement();
            }
            else if (classificationMode ==
                     TemperatureClassificationMode.ExactDecisionFallback)
            {
                classification = TemperatureEligibilityClassKey
                    .ExactDecisionBucket(
                        TemperatureDecisionBucket.FromTemperature(
                            temperatureKelvin));
            }
            else
            {
                TemperaturePartitionDefinition? partitionDefinition =
                    GetOrCreateApplicableTemperaturePartitionDefinition(
                        tagIdentity,
                        applicableRequestedTags);
                classification = partitionDefinition == null
                    ? TemperatureEligibilityClassKey.NoTemperatureDistinction()
                    : TemperatureEligibilityClassKey
                        .OptimizedPartitionInterval(
                            partitionDefinition.DefinitionId,
                            partitionDefinition.Classify(
                                TemperatureDecisionBucket.FromTemperature(
                                    temperatureKelvin)));
            }

            // Cache the complete kind-aware key, not only an ordinal. Comparator
            // and duplicate-suppression adapters must observe the same result even
            // if a mutable candidate field changes later in this update.
            temperatureEligibilityClassByPickupInstanceId.Add(
                pickupInstanceId,
                classification);
            return classification;
        }

        internal void Complete() => CompleteOrDiscard();

        internal void Discard() => CompleteOrDiscard();

        private TemperaturePartitionDefinition?
            GetOrCreateApplicableTemperaturePartitionDefinition(
                PickupTagIdentity tagIdentity,
                IReadOnlyList<Tag> applicableRequestedTags)
        {
            if (firstApplicableRequestedTagPartitionResolutionByPickupTagIdentity
                .TryGetValue(tagIdentity, out var firstPartition))
            {
                ApplicableRequestedTagPartitionResolution?
                    candidatePartitionResolution =
                    firstPartition;
                ApplicableRequestedTagPartitionResolution?
                    lastPartitionResolution = null;
                while (candidatePartitionResolution != null)
                {
                    if (candidatePartitionResolution.Matches(
                            applicableRequestedTags))
                    {
                        return candidatePartitionResolution.PartitionDefinition;
                    }

                    lastPartitionResolution = candidatePartitionResolution;
                    candidatePartitionResolution =
                        candidatePartitionResolution.NextResolution;
                }

                var additionalPartitionResolution =
                    CreateApplicableRequestedTagPartitionResolution(
                        applicableRequestedTags);
                lastPartitionResolution!.NextResolution =
                    additionalPartitionResolution;
                return additionalPartitionResolution.PartitionDefinition;
            }

            var newFirstPartitionResolution =
                CreateApplicableRequestedTagPartitionResolution(
                    applicableRequestedTags);
            firstApplicableRequestedTagPartitionResolutionByPickupTagIdentity.Add(
                tagIdentity,
                newFirstPartitionResolution);
            return newFirstPartitionResolution.PartitionDefinition;
        }

        private ApplicableRequestedTagPartitionResolution
            CreateApplicableRequestedTagPartitionResolution(
                IReadOnlyList<Tag> applicableRequestedTags)
        {
            Tag[] normalizedApplicableRequestedTags =
                CreateDistinctTagsInFirstEncounterOrder(
                    applicableRequestedTags);
            IReadOnlyList<int> sortedDecisionEndpointsKelvin =
                capturedEligibilitySnapshot!
                    .CreateSortedDecisionEndpointUnion(
                        resolvedParentWorldId!.Value,
                        normalizedApplicableRequestedTags);
            TemperaturePartitionDefinition? partitionDefinition =
                sortedDecisionEndpointsKelvin.Count == 0
                    ? null
                    : GetOrCreateTemperaturePartitionDefinition(
                        sortedDecisionEndpointsKelvin);
            return new ApplicableRequestedTagPartitionResolution(
                normalizedApplicableRequestedTags,
                partitionDefinition);
        }

        private TemperaturePartitionDefinition
            GetOrCreateTemperaturePartitionDefinition(
                IReadOnlyList<int> sortedDecisionEndpointsKelvin)
        {
            var lookupKey = new SortedDecisionEndpointSequenceKey(
                sortedDecisionEndpointsKelvin);
            if (temperaturePartitionDefinitionByDecisionEndpoints.TryGetValue(
                    lookupKey,
                    out var existingDefinition))
            {
                return existingDefinition;
            }

            int nextDefinitionId = GetNextPartitionDefinitionId();
            TemperaturePartitionDefinition definition =
                TemperaturePartitionDefinition.Create(
                    nextDefinitionId,
                    sortedDecisionEndpointsKelvin);

            // Retain only the definition-owned immutable sequence. The lookup may
            // have been a newly allocated multi-tag union owned by the snapshot
            // caller; keeping a second reference would duplicate per-update state.
            var retainedKey = new SortedDecisionEndpointSequenceKey(
                definition.SortedDecisionEndpointsKelvin);
            temperaturePartitionDefinitionByDecisionEndpoints.Add(
                retainedKey,
                definition);
            lastAssignedPartitionDefinitionId = nextDefinitionId;
            return definition;
        }

        private int GetNextPartitionDefinitionId()
        {
            if (lastAssignedPartitionDefinitionId == int.MaxValue)
            {
                throw new InvalidOperationException(
                    "The pickup-update temperature partition definition identity " +
                    "space is exhausted; identities will not wrap or be reused.");
            }

            try
            {
                int nextDefinitionId = checked(
                    lastAssignedPartitionDefinitionId + 1);
                if (nextDefinitionId <= 0)
                {
                    throw new InvalidOperationException(
                        "The pickup-update temperature partition definition " +
                        "identity space is exhausted; identities will not wrap " +
                        "or be reused.");
                }

                return nextDefinitionId;
            }
            catch (OverflowException exception)
            {
                throw new InvalidOperationException(
                    "The pickup-update temperature partition definition identity " +
                    "space is exhausted; identities will not wrap or be reused.",
                    exception);
            }
        }

        private void CompleteOrDiscard()
        {
            if (!isActive)
            {
                return;
            }

            int priorPickupClassificationCount =
                temperatureEligibilityClassByPickupInstanceId.Count;

            // Mark inactive and release the graph roots before retaining or
            // replacing reusable backing stores. No Unity object is ever stored by
            // this pure session, but the captured game session and snapshots own
            // colony-scale state and must not outlive one grouping update.
            isActive = false;
            capturedGameSession = null;
            capturedActiveTemperatureConstraints = null;
            capturedEligibilitySnapshot = null;
            capturedWorldTopology = null;
            resolvedParentWorldId = null;
            classificationMode = default(TemperatureClassificationMode);
            lastAssignedPartitionDefinitionId = 0;

            if (priorPickupClassificationCount >
                RetainedCollectionCapacityLimits
                    .MaximumRetainedPickupClassificationCount)
            {
                temperatureEligibilityClassByPickupInstanceId =
                    new Dictionary<int, TemperatureEligibilityClassKey>();
                firstApplicableRequestedTagPartitionResolutionByPickupTagIdentity =
                    new Dictionary<
                        PickupTagIdentity,
                        ApplicableRequestedTagPartitionResolution>();
                temperaturePartitionDefinitionByDecisionEndpoints =
                    new Dictionary<
                        SortedDecisionEndpointSequenceKey,
                        TemperaturePartitionDefinition>();
                return;
            }

            temperatureEligibilityClassByPickupInstanceId.Clear();
            firstApplicableRequestedTagPartitionResolutionByPickupTagIdentity
                .Clear();
            temperaturePartitionDefinitionByDecisionEndpoints.Clear();
        }

        private static Tag[] CreateDistinctTagsInFirstEncounterOrder(
            IReadOnlyList<Tag> applicableRequestedTags)
        {
            int distinctTagCount = 0;
            for (int candidateTagIndex = 0;
                 candidateTagIndex < applicableRequestedTags.Count;
                 candidateTagIndex++)
            {
                if (!TagAppearsBefore(
                        applicableRequestedTags,
                        candidateTagIndex))
                {
                    distinctTagCount++;
                }
            }

            if (distinctTagCount == 0)
            {
                return Array.Empty<Tag>();
            }

            var distinctTags = new Tag[distinctTagCount];
            int destinationTagIndex = 0;
            for (int candidateTagIndex = 0;
                 candidateTagIndex < applicableRequestedTags.Count;
                 candidateTagIndex++)
            {
                if (TagAppearsBefore(
                        applicableRequestedTags,
                        candidateTagIndex))
                {
                    continue;
                }

                distinctTags[destinationTagIndex] =
                    applicableRequestedTags[candidateTagIndex];
                destinationTagIndex++;
            }

            return distinctTags;
        }

        private static bool TagAppearsBefore(
            IReadOnlyList<Tag> tags,
            int candidateTagIndex)
        {
            Tag candidateTag = tags[candidateTagIndex];
            for (int priorTagIndex = 0;
                 priorTagIndex < candidateTagIndex;
                 priorTagIndex++)
            {
                if (tags[priorTagIndex].Equals(candidateTag))
                {
                    return true;
                }
            }

            return false;
        }

        private enum TemperatureClassificationMode
        {
            NoTemperatureDistinction,
            CurrentScopedSnapshot,
            ExactDecisionFallback
        }

        private sealed class ApplicableRequestedTagPartitionResolution
        {
            private readonly Tag[] normalizedApplicableRequestedTags;

            internal ApplicableRequestedTagPartitionResolution(
                Tag[] normalizedApplicableRequestedTags,
                TemperaturePartitionDefinition? partitionDefinition)
            {
                this.normalizedApplicableRequestedTags =
                    normalizedApplicableRequestedTags;
                PartitionDefinition = partitionDefinition;
            }

            internal TemperaturePartitionDefinition? PartitionDefinition
            {
                get;
            }

            internal ApplicableRequestedTagPartitionResolution? NextResolution
            {
                get;
                set;
            }

            internal bool Matches(
                IReadOnlyList<Tag> applicableRequestedTags)
            {
                // The runtime adapter supplies an already-distinct frozen list, so
                // this exact-sequence path is the ordinary O(tag-count) case.
                if (applicableRequestedTags.Count ==
                    normalizedApplicableRequestedTags.Length)
                {
                    for (int tagIndex = 0;
                         tagIndex < normalizedApplicableRequestedTags.Length;
                         tagIndex++)
                    {
                        if (!normalizedApplicableRequestedTags[tagIndex].Equals(
                                applicableRequestedTags[tagIndex]))
                        {
                            return false;
                        }
                    }

                    return true;
                }

                // Defensive callers may repeat a tag. Compare the same
                // first-encounter normalization without allocating on a cache hit.
                int normalizedTagIndex = 0;
                for (int candidateTagIndex = 0;
                     candidateTagIndex < applicableRequestedTags.Count;
                     candidateTagIndex++)
                {
                    if (TagAppearsBefore(
                            applicableRequestedTags,
                            candidateTagIndex))
                    {
                        continue;
                    }

                    if (normalizedTagIndex >=
                            normalizedApplicableRequestedTags.Length ||
                        !normalizedApplicableRequestedTags[normalizedTagIndex]
                            .Equals(applicableRequestedTags[candidateTagIndex]))
                    {
                        return false;
                    }

                    normalizedTagIndex++;
                }

                return normalizedTagIndex ==
                    normalizedApplicableRequestedTags.Length;
            }
        }

        private readonly struct SortedDecisionEndpointSequenceKey :
            IEquatable<SortedDecisionEndpointSequenceKey>
        {
            private readonly IReadOnlyList<int>? sortedDecisionEndpointsKelvin;
            private readonly int hashCode;

            internal SortedDecisionEndpointSequenceKey(
                IReadOnlyList<int> sortedDecisionEndpointsKelvin)
            {
                this.sortedDecisionEndpointsKelvin =
                    sortedDecisionEndpointsKelvin ??
                    throw new ArgumentNullException(
                        nameof(sortedDecisionEndpointsKelvin));

                unchecked
                {
                    int sequenceHashCode = 17;
                    for (int endpointIndex = 0;
                         endpointIndex < sortedDecisionEndpointsKelvin.Count;
                         endpointIndex++)
                    {
                        sequenceHashCode = (sequenceHashCode * 31) ^
                            sortedDecisionEndpointsKelvin[endpointIndex];
                    }

                    hashCode = sequenceHashCode;
                }
            }

            public bool Equals(SortedDecisionEndpointSequenceKey other)
            {
                if (hashCode != other.hashCode ||
                    sortedDecisionEndpointsKelvin == null ||
                    other.sortedDecisionEndpointsKelvin == null ||
                    sortedDecisionEndpointsKelvin.Count !=
                        other.sortedDecisionEndpointsKelvin.Count)
                {
                    return false;
                }

                for (int endpointIndex = 0;
                     endpointIndex < sortedDecisionEndpointsKelvin.Count;
                     endpointIndex++)
                {
                    if (sortedDecisionEndpointsKelvin[endpointIndex] !=
                        other.sortedDecisionEndpointsKelvin[endpointIndex])
                    {
                        return false;
                    }
                }

                return true;
            }

            public override bool Equals(object? obj) =>
                obj is SortedDecisionEndpointSequenceKey other && Equals(other);

            public override int GetHashCode() => hashCode;
        }
    }
}
