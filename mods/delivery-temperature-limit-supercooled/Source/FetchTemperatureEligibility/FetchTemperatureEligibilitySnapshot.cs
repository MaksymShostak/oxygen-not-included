#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Complete immutable storage and pickup temperature facts for one version set.
    /// </summary>
    internal sealed class FetchTemperatureEligibilitySnapshot
    {
        private static readonly IReadOnlyList<int> EmptyDecisionEndpointsKelvin =
            Array.AsReadOnly(Array.Empty<int>());
        private static readonly IReadOnlyList<Tag> EmptyRequestedTags =
            Array.AsReadOnly(Array.Empty<Tag>());

        private readonly Dictionary<
            ParentWorldRequestedTagKey,
            AllowedTemperatureIntervalSet>
                storageEligibilityByParentWorldAndRequestedTag;
        private readonly Dictionary<
            ParentWorldRequestedTagKey,
            IReadOnlyList<int>>
                sortedDecisionEndpointsKelvinByParentWorldAndRequestedTag;
        private readonly Dictionary<int, IReadOnlyList<Tag>>
            requestedTagsByParentWorldId;

        internal FetchTemperatureEligibilitySnapshot(
            GameSessionGeneration gameSessionGeneration,
            TemperatureConstraintGeneration constraintGeneration,
            FetchRequestTopologyVersion fetchTopologyVersion,
            WorldParentTopologyVersion worldTopologyVersion,
            Dictionary<
                ParentWorldRequestedTagKey,
                AllowedTemperatureIntervalSet>
                    storageEligibilityByParentWorldAndRequestedTag,
            Dictionary<
                ParentWorldRequestedTagKey,
                IReadOnlyList<int>>
                    sortedDecisionEndpointsKelvinByParentWorldAndRequestedTag,
            Dictionary<int, IReadOnlyList<Tag>> requestedTagsByParentWorldId)
        {
            if (gameSessionGeneration.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(gameSessionGeneration),
                    "A fetch-temperature snapshot requires a positive game-session " +
                    "generation.");
            }

            this.storageEligibilityByParentWorldAndRequestedTag =
                storageEligibilityByParentWorldAndRequestedTag ??
                throw new ArgumentNullException(
                    nameof(storageEligibilityByParentWorldAndRequestedTag));
            this.sortedDecisionEndpointsKelvinByParentWorldAndRequestedTag =
                sortedDecisionEndpointsKelvinByParentWorldAndRequestedTag ??
                throw new ArgumentNullException(
                    nameof(
                        sortedDecisionEndpointsKelvinByParentWorldAndRequestedTag));
            this.requestedTagsByParentWorldId =
                requestedTagsByParentWorldId ??
                throw new ArgumentNullException(nameof(requestedTagsByParentWorldId));

            if (storageEligibilityByParentWorldAndRequestedTag.Count !=
                sortedDecisionEndpointsKelvinByParentWorldAndRequestedTag.Count)
            {
                throw new ArgumentException(
                    "Storage eligibility and pickup endpoint projections must cover " +
                    "the same parent-world/requested-tag entries.");
            }

            GameSessionGeneration = gameSessionGeneration;
            ConstraintGeneration = constraintGeneration;
            FetchTopologyVersion = fetchTopologyVersion;
            WorldTopologyVersion = worldTopologyVersion;
        }

        internal GameSessionGeneration GameSessionGeneration { get; }

        internal TemperatureConstraintGeneration ConstraintGeneration { get; }

        internal FetchRequestTopologyVersion FetchTopologyVersion { get; }

        internal WorldParentTopologyVersion WorldTopologyVersion { get; }

        internal bool TryGetStorageEligibility(
            int parentWorldId,
            Tag requestedTag,
            out AllowedTemperatureIntervalSet intervals)
        {
            if (storageEligibilityByParentWorldAndRequestedTag.TryGetValue(
                    new ParentWorldRequestedTagKey(parentWorldId, requestedTag),
                    out var resolvedIntervals))
            {
                intervals = resolvedIntervals;
                return true;
            }

            intervals = null!;
            return false;
        }

        internal IReadOnlyList<Tag> GetRequestedTags(int parentWorldId) =>
            requestedTagsByParentWorldId.TryGetValue(
                parentWorldId,
                out var requestedTags)
                ? requestedTags
                : EmptyRequestedTags;

        internal IReadOnlyList<int> CreateSortedDecisionEndpointUnion(
            int parentWorldId,
            IReadOnlyList<Tag> applicableRequestedTags)
        {
            if (applicableRequestedTags == null)
            {
                throw new ArgumentNullException(nameof(applicableRequestedTags));
            }

            int matchedNonEmptySequenceCount = 0;
            int combinedEndpointCount = 0;
            IReadOnlyList<int>? singleMatchedSequence = null;
            for (int tagIndex = 0;
                 tagIndex < applicableRequestedTags.Count;
                 tagIndex++)
            {
                if (!sortedDecisionEndpointsKelvinByParentWorldAndRequestedTag
                    .TryGetValue(
                        new ParentWorldRequestedTagKey(
                            parentWorldId,
                            applicableRequestedTags[tagIndex]),
                        out var endpoints) ||
                    endpoints.Count == 0)
                {
                    continue;
                }

                matchedNonEmptySequenceCount++;
                singleMatchedSequence = endpoints;
                combinedEndpointCount = checked(
                    combinedEndpointCount + endpoints.Count);
            }

            if (matchedNonEmptySequenceCount == 0)
            {
                return EmptyDecisionEndpointsKelvin;
            }

            if (matchedNonEmptySequenceCount == 1)
            {
                // The snapshot already owns and read-only-publishes this exact array;
                // returning it avoids a needless per-identity union allocation.
                return singleMatchedSequence!;
            }

            var combinedEndpointsKelvin = new int[combinedEndpointCount];
            int destinationEndpointIndex = 0;
            for (int tagIndex = 0;
                 tagIndex < applicableRequestedTags.Count;
                 tagIndex++)
            {
                if (!sortedDecisionEndpointsKelvinByParentWorldAndRequestedTag
                    .TryGetValue(
                        new ParentWorldRequestedTagKey(
                            parentWorldId,
                            applicableRequestedTags[tagIndex]),
                        out var endpoints))
                {
                    continue;
                }

                for (int endpointIndex = 0;
                     endpointIndex < endpoints.Count;
                     endpointIndex++)
                {
                    combinedEndpointsKelvin[destinationEndpointIndex] =
                        endpoints[endpointIndex];
                    destinationEndpointIndex++;
                }
            }

            Array.Sort(combinedEndpointsKelvin);
            int uniqueEndpointCount = 1;
            for (int endpointIndex = 1;
                 endpointIndex < combinedEndpointsKelvin.Length;
                 endpointIndex++)
            {
                if (combinedEndpointsKelvin[endpointIndex] ==
                    combinedEndpointsKelvin[uniqueEndpointCount - 1])
                {
                    continue;
                }

                combinedEndpointsKelvin[uniqueEndpointCount] =
                    combinedEndpointsKelvin[endpointIndex];
                uniqueEndpointCount++;
            }

            if (uniqueEndpointCount != combinedEndpointsKelvin.Length)
            {
                var exactCombinedEndpointsKelvin = new int[uniqueEndpointCount];
                Array.Copy(
                    combinedEndpointsKelvin,
                    exactCombinedEndpointsKelvin,
                    uniqueEndpointCount);
                combinedEndpointsKelvin = exactCombinedEndpointsKelvin;
            }

            return Array.AsReadOnly(combinedEndpointsKelvin);
        }

        internal readonly struct ParentWorldRequestedTagKey :
            IEquatable<ParentWorldRequestedTagKey>
        {
            internal ParentWorldRequestedTagKey(
                int parentWorldId,
                Tag requestedTag)
            {
                ParentWorldId = parentWorldId;
                RequestedTag = requestedTag;
            }

            internal int ParentWorldId { get; }

            internal Tag RequestedTag { get; }

            public bool Equals(ParentWorldRequestedTagKey other) =>
                ParentWorldId == other.ParentWorldId &&
                RequestedTag.Equals(other.RequestedTag);

            public override bool Equals(object? obj) =>
                obj is ParentWorldRequestedTagKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (ParentWorldId * 397) ^ RequestedTag.GetHashCode();
                }
            }
        }
    }
}
