#nullable enable

using System;
using System.Collections.Generic;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Reusable authoritative-traversal builder for both fetch eligibility views.
    /// </summary>
    internal sealed class FetchTemperatureEligibilityBuilder
    {
        private Dictionary<
            FetchTemperatureEligibilitySnapshot.ParentWorldRequestedTagKey,
            MutableDestinationRequirements>
                destinationRequirementsByParentWorldAndRequestedTag =
                    new Dictionary<
                        FetchTemperatureEligibilitySnapshot
                            .ParentWorldRequestedTagKey,
                        MutableDestinationRequirements>();
        private List<
            FetchTemperatureEligibilitySnapshot.ParentWorldRequestedTagKey>
                destinationRequirementKeysInFirstEncounterOrder =
                    new List<
                        FetchTemperatureEligibilitySnapshot
                            .ParentWorldRequestedTagKey>();
        private HashSet<Tag> distinctRequestedTagsInCurrentRequest =
            new HashSet<Tag>();

        private bool isBuilding;
        private GameSessionGeneration gameSessionGeneration;
        private TemperatureConstraintGeneration constraintGeneration;
        private FetchRequestTopologyVersion fetchTopologyVersion;
        private WorldParentTopologyVersion worldTopologyVersion;

        internal void Begin(
            GameSessionGeneration gameSessionGeneration,
            ActiveTemperatureConstraintSnapshot constraints,
            FetchRequestTopologyVersion fetchTopologyVersion,
            WorldParentTopologySnapshot worldTopology)
        {
            if (isBuilding)
            {
                throw new InvalidOperationException(
                    "A fetch-temperature eligibility build is already active.");
            }

            if (gameSessionGeneration.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(gameSessionGeneration));
            }

            if (constraints == null)
            {
                throw new ArgumentNullException(nameof(constraints));
            }

            if (worldTopology == null)
            {
                throw new ArgumentNullException(nameof(worldTopology));
            }

            if (!worldTopology.GameSessionGeneration.Equals(gameSessionGeneration))
            {
                throw new ArgumentException(
                    "The world topology must belong to the same game-session " +
                    "generation as the fetch eligibility build.",
                    nameof(worldTopology));
            }

            this.gameSessionGeneration = gameSessionGeneration;
            constraintGeneration = constraints.Generation;
            this.fetchTopologyVersion = fetchTopologyVersion;
            worldTopologyVersion = worldTopology.Version;
            isBuilding = true;
        }

        internal void AddUnconstrainedFetchRequest(
            int parentWorldId,
            IReadOnlyList<Tag> requestedTags)
        {
            ThrowIfNotBuilding();
            ValidateParentWorldId(parentWorldId);
            if (requestedTags == null)
            {
                throw new ArgumentNullException(nameof(requestedTags));
            }

            AddDistinctRequestedTags(
                parentWorldId,
                requestedTags,
                enabledConstraint: null);
        }

        internal void AddTemperatureConstrainedFetchRequest(
            int parentWorldId,
            IReadOnlyList<Tag> requestedTags,
            DeliveryTemperatureConstraint enabledConstraint)
        {
            ThrowIfNotBuilding();
            ValidateParentWorldId(parentWorldId);
            if (requestedTags == null)
            {
                throw new ArgumentNullException(nameof(requestedTags));
            }

            if (!enabledConstraint.IsEnabled)
            {
                throw new ArgumentException(
                    "enabledConstraint must be an enabled delivery temperature " +
                    "constraint; use AddUnconstrainedFetchRequest for an " +
                    "unconstrained destination.",
                    nameof(enabledConstraint));
            }

            AddDistinctRequestedTags(
                parentWorldId,
                requestedTags,
                enabledConstraint);
        }

        internal FetchTemperatureEligibilitySnapshot Build()
        {
            ThrowIfNotBuilding();

            int entryCount =
                destinationRequirementsByParentWorldAndRequestedTag.Count;
            var storageEligibilityByParentWorldAndRequestedTag =
                new Dictionary<
                    FetchTemperatureEligibilitySnapshot
                        .ParentWorldRequestedTagKey,
                    AllowedTemperatureIntervalSet>(entryCount);
            var sortedDecisionEndpointsKelvinByParentWorldAndRequestedTag =
                new Dictionary<
                    FetchTemperatureEligibilitySnapshot
                        .ParentWorldRequestedTagKey,
                    IReadOnlyList<int>>(entryCount);
            var mutableRequestedTagsByParentWorldId =
                new Dictionary<int, List<Tag>>();

            // This unique first-encounter sequence is the one authoritative traversal
            // for all projections. It avoids depending on Dictionary enumeration or
            // inventing an ordering for ONI Tag values merely to publish stable tags.
            for (int keyIndex = 0;
                 keyIndex <
                    destinationRequirementKeysInFirstEncounterOrder.Count;
                 keyIndex++)
            {
                var key =
                    destinationRequirementKeysInFirstEncounterOrder[keyIndex];
                MutableDestinationRequirements requirements =
                    destinationRequirementsByParentWorldAndRequestedTag[key];

                storageEligibilityByParentWorldAndRequestedTag.Add(
                    key,
                    AllowedTemperatureIntervalSet.CreateFromDestinations(
                        requirements.IncludesUnconstrainedDestination,
                        requirements.EnabledDestinationConstraints));
                sortedDecisionEndpointsKelvinByParentWorldAndRequestedTag.Add(
                    key,
                    requirements.CreateSortedDecisionEndpointsKelvin());

                if (!mutableRequestedTagsByParentWorldId.TryGetValue(
                        key.ParentWorldId,
                        out var requestedTags))
                {
                    requestedTags = new List<Tag>();
                    mutableRequestedTagsByParentWorldId.Add(
                        key.ParentWorldId,
                        requestedTags);
                }

                requestedTags.Add(key.RequestedTag);
            }

            var requestedTagsByParentWorldId =
                new Dictionary<int, IReadOnlyList<Tag>>(
                    mutableRequestedTagsByParentWorldId.Count);
            foreach (KeyValuePair<int, List<Tag>> parentRequestedTags in
                     mutableRequestedTagsByParentWorldId)
            {
                requestedTagsByParentWorldId.Add(
                    parentRequestedTags.Key,
                    Array.AsReadOnly(parentRequestedTags.Value.ToArray()));
            }

            var snapshot = new FetchTemperatureEligibilitySnapshot(
                gameSessionGeneration,
                constraintGeneration,
                fetchTopologyVersion,
                worldTopologyVersion,
                storageEligibilityByParentWorldAndRequestedTag,
                sortedDecisionEndpointsKelvinByParentWorldAndRequestedTag,
                requestedTagsByParentWorldId);
            CompleteOrDiscardBuild(entryCount);
            return snapshot;
        }

        internal void Discard()
        {
            int entryCount =
                destinationRequirementsByParentWorldAndRequestedTag.Count;
            CompleteOrDiscardBuild(entryCount);
        }

        private void AddDistinctRequestedTags(
            int parentWorldId,
            IReadOnlyList<Tag> requestedTags,
            DeliveryTemperatureConstraint? enabledConstraint)
        {
            distinctRequestedTagsInCurrentRequest.Clear();
            try
            {
                for (int tagIndex = 0;
                     tagIndex < requestedTags.Count;
                     tagIndex++)
                {
                    Tag requestedTag = requestedTags[tagIndex];
                    if (!distinctRequestedTagsInCurrentRequest.Add(requestedTag))
                    {
                        continue;
                    }

                    var key = new FetchTemperatureEligibilitySnapshot
                        .ParentWorldRequestedTagKey(
                            parentWorldId,
                            requestedTag);
                    if (!destinationRequirementsByParentWorldAndRequestedTag
                        .TryGetValue(key, out var requirements))
                    {
                        requirements = new MutableDestinationRequirements();
                        destinationRequirementsByParentWorldAndRequestedTag.Add(
                            key,
                            requirements);
                        destinationRequirementKeysInFirstEncounterOrder.Add(key);
                    }

                    if (enabledConstraint.HasValue)
                    {
                        requirements.AddEnabledDestinationConstraint(
                            enabledConstraint.Value);
                    }
                    else
                    {
                        requirements.IncludesUnconstrainedDestination = true;
                    }
                }
            }
            finally
            {
                // A throwing IReadOnlyList can leave deliberate partial candidate
                // state for Discard to release, but it must never pin this request's
                // temporary deduplication keys independently.
                distinctRequestedTagsInCurrentRequest.Clear();
            }
        }

        private void CompleteOrDiscardBuild(int priorEntryCount)
        {
            isBuilding = false;
            gameSessionGeneration = default(GameSessionGeneration);
            constraintGeneration = default(TemperatureConstraintGeneration);
            fetchTopologyVersion = default(FetchRequestTopologyVersion);
            worldTopologyVersion = default(WorldParentTopologyVersion);

            if (priorEntryCount >
                RetainedCollectionCapacityLimits
                    .MaximumRetainedFetchEligibilityEntryCount)
            {
                // Replace, rather than Clear, after an oversized build so a single
                // pathological colony update cannot pin peak dictionary/list backing
                // storage for the rest of the game session.
                destinationRequirementsByParentWorldAndRequestedTag =
                    new Dictionary<
                        FetchTemperatureEligibilitySnapshot
                            .ParentWorldRequestedTagKey,
                        MutableDestinationRequirements>();
                destinationRequirementKeysInFirstEncounterOrder =
                    new List<
                        FetchTemperatureEligibilitySnapshot
                            .ParentWorldRequestedTagKey>();
                distinctRequestedTagsInCurrentRequest = new HashSet<Tag>();
                return;
            }

            destinationRequirementsByParentWorldAndRequestedTag.Clear();
            destinationRequirementKeysInFirstEncounterOrder.Clear();
            distinctRequestedTagsInCurrentRequest.Clear();
        }

        private void ThrowIfNotBuilding()
        {
            if (!isBuilding)
            {
                throw new InvalidOperationException(
                    "Begin must start a fetch-temperature eligibility build before " +
                    "requests can be added or a snapshot can be built.");
            }
        }

        private static void ValidateParentWorldId(int parentWorldId)
        {
            if (parentWorldId < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(parentWorldId),
                    parentWorldId,
                    "An ONI parent-world identity cannot be negative.");
            }
        }

        private sealed class MutableDestinationRequirements
        {
            private static readonly IReadOnlyList<
                DeliveryTemperatureConstraint> EmptyEnabledConstraints =
                    Array.AsReadOnly(
                        Array.Empty<DeliveryTemperatureConstraint>());
            private static readonly IReadOnlyList<int>
                EmptySortedDecisionEndpointsKelvin =
                    Array.AsReadOnly(Array.Empty<int>());

            private List<DeliveryTemperatureConstraint>?
                enabledDestinationConstraints;

            internal bool IncludesUnconstrainedDestination { get; set; }

            internal IReadOnlyList<DeliveryTemperatureConstraint>
                EnabledDestinationConstraints =>
                    enabledDestinationConstraints ?? EmptyEnabledConstraints;

            internal void AddEnabledDestinationConstraint(
                DeliveryTemperatureConstraint enabledConstraint)
            {
                // Allocate finite-constraint storage only for an entry that actually
                // has one. Unconstrained-only parent/tag entries are common and must
                // not pay for two otherwise-unused mutable collections.
                if (enabledDestinationConstraints == null)
                {
                    enabledDestinationConstraints =
                        new List<DeliveryTemperatureConstraint>();
                }

                enabledDestinationConstraints.Add(enabledConstraint);
            }

            internal IReadOnlyList<int> CreateSortedDecisionEndpointsKelvin()
            {
                if (enabledDestinationConstraints == null)
                {
                    return EmptySortedDecisionEndpointsKelvin;
                }

                int nonEmptyConstraintCount = 0;
                for (int constraintIndex = 0;
                     constraintIndex < enabledDestinationConstraints.Count;
                     constraintIndex++)
                {
                    if (!enabledDestinationConstraints[constraintIndex].IsEmpty)
                    {
                        nonEmptyConstraintCount++;
                    }
                }

                if (nonEmptyConstraintCount == 0)
                {
                    return EmptySortedDecisionEndpointsKelvin;
                }

                // Pickup endpoints remain independent from the normalized storage
                // union. In particular, an unconstrained storage destination must
                // not erase constrained construction/fetch destinations for the
                // same tag. Sorting one exact temporary array also avoids retaining
                // a HashSet for every active builder entry.
                var sortedDecisionEndpointsKelvin = new int[checked(
                    nonEmptyConstraintCount * 2)];
                int endpointIndex = 0;
                for (int constraintIndex = 0;
                     constraintIndex < enabledDestinationConstraints.Count;
                     constraintIndex++)
                {
                    DeliveryTemperatureConstraint enabledConstraint =
                        enabledDestinationConstraints[constraintIndex];
                    if (enabledConstraint.IsEmpty)
                    {
                        continue;
                    }

                    sortedDecisionEndpointsKelvin[endpointIndex] =
                        enabledConstraint.MinimumInclusiveKelvin;
                    endpointIndex++;
                    sortedDecisionEndpointsKelvin[endpointIndex] =
                        enabledConstraint.MaximumExclusiveKelvin;
                    endpointIndex++;
                }

                Array.Sort(sortedDecisionEndpointsKelvin);
                int uniqueEndpointCount = 1;
                for (endpointIndex = 1;
                     endpointIndex < sortedDecisionEndpointsKelvin.Length;
                     endpointIndex++)
                {
                    if (sortedDecisionEndpointsKelvin[endpointIndex] ==
                        sortedDecisionEndpointsKelvin[uniqueEndpointCount - 1])
                    {
                        continue;
                    }

                    sortedDecisionEndpointsKelvin[uniqueEndpointCount] =
                        sortedDecisionEndpointsKelvin[endpointIndex];
                    uniqueEndpointCount++;
                }

                if (uniqueEndpointCount != sortedDecisionEndpointsKelvin.Length)
                {
                    var exactSortedDecisionEndpointsKelvin =
                        new int[uniqueEndpointCount];
                    Array.Copy(
                        sortedDecisionEndpointsKelvin,
                        exactSortedDecisionEndpointsKelvin,
                        uniqueEndpointCount);
                    sortedDecisionEndpointsKelvin =
                        exactSortedDecisionEndpointsKelvin;
                }

                return Array.AsReadOnly(sortedDecisionEndpointsKelvin);
            }
        }
    }
}
