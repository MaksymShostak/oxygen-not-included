#nullable enable

using System;
using System.Collections.Generic;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Complete immutable world-to-parent topology captured by hot-path readers.
    /// Both lookup directions are constructed before one reference is published, so
    /// a reader can resolve a parent and enumerate its members from the same state.
    /// </summary>
    internal sealed class WorldParentTopologySnapshot
    {
        private static readonly IReadOnlyList<int> EmptyMemberWorldIds =
            Array.AsReadOnly(Array.Empty<int>());

        private readonly Dictionary<int, int> parentWorldIdsByWorldId;
        private readonly Dictionary<int, IReadOnlyList<int>>
            memberWorldIdsByParentWorldId;

        internal WorldParentTopologySnapshot(
            GameSessionGeneration gameSessionGeneration,
            WorldParentTopologyVersion version,
            IReadOnlyDictionary<int, int> sourceParentWorldIdsByWorldId)
        {
            if (gameSessionGeneration.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(gameSessionGeneration),
                    "A world-parent topology snapshot requires a nonzero " +
                    "game-session generation.");
            }

            if (sourceParentWorldIdsByWorldId is null)
            {
                throw new ArgumentNullException(
                    nameof(sourceParentWorldIdsByWorldId));
            }

            GameSessionGeneration = gameSessionGeneration;
            Version = version;
            parentWorldIdsByWorldId =
                new Dictionary<int, int>(sourceParentWorldIdsByWorldId.Count);
            var mutableMemberWorldIdsByParentWorldId =
                new Dictionary<int, List<int>>();

            foreach (var mapping in sourceParentWorldIdsByWorldId)
            {
                parentWorldIdsByWorldId.Add(mapping.Key, mapping.Value);
                if (!mutableMemberWorldIdsByParentWorldId.TryGetValue(
                        mapping.Value,
                        out var memberWorldIds))
                {
                    memberWorldIds = new List<int>();
                    mutableMemberWorldIdsByParentWorldId.Add(
                        mapping.Value,
                        memberWorldIds);
                }

                memberWorldIds.Add(mapping.Key);
            }

            memberWorldIdsByParentWorldId =
                new Dictionary<int, IReadOnlyList<int>>(
                    mutableMemberWorldIdsByParentWorldId.Count);
            foreach (var parentMembers in
                     mutableMemberWorldIdsByParentWorldId)
            {
                parentMembers.Value.Sort();
                memberWorldIdsByParentWorldId.Add(
                    parentMembers.Key,
                    Array.AsReadOnly(parentMembers.Value.ToArray()));
            }
        }

        internal GameSessionGeneration GameSessionGeneration { get; }

        internal WorldParentTopologyVersion Version { get; }

        internal bool TryResolveParentWorld(
            int worldId,
            out int parentWorldId) =>
            parentWorldIdsByWorldId.TryGetValue(
                worldId,
                out parentWorldId);

        internal IReadOnlyList<int> GetMemberWorldIds(int parentWorldId)
        {
            return memberWorldIdsByParentWorldId.TryGetValue(
                parentWorldId,
                out var memberWorldIds)
                ? memberWorldIds
                : EmptyMemberWorldIds;
        }
    }
}
