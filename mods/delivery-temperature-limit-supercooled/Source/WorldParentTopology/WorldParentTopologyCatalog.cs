#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Owns the main-thread world-parent mapping and publishes whole immutable
    /// snapshots for lock-free worker readers. It is content-mode neutral: callers
    /// provide authoritative ONI identities and relationships without DLC guesses.
    /// </summary>
    internal sealed class WorldParentTopologyCatalog
    {
        private readonly object topologyMutationLock = new object();
        private readonly GameSessionGeneration gameSessionGeneration;

        // This is the catalog's sole mutable mapping. Published snapshots always own
        // separate copies, so shutdown may clear this dictionary without mutating a
        // snapshot already captured by a worker.
        private Dictionary<int, int> parentWorldIdsByWorldId;
        private bool acceptsTopologyPublications = true;
        private long version;
        private WorldParentTopologySnapshot publishedSnapshot;

        internal WorldParentTopologyCatalog(
            GameSessionGeneration gameSessionGeneration)
        {
            if (gameSessionGeneration.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(gameSessionGeneration),
                    "A world-parent topology catalog requires a nonzero " +
                    "game-session generation.");
            }

            this.gameSessionGeneration = gameSessionGeneration;
            parentWorldIdsByWorldId = new Dictionary<int, int>();
            publishedSnapshot = new WorldParentTopologySnapshot(
                gameSessionGeneration,
                new WorldParentTopologyVersion(0),
                parentWorldIdsByWorldId);
        }

        internal WorldParentTopologySnapshot CaptureSnapshot() =>
            Volatile.Read(ref publishedSnapshot);

        internal WorldParentTopologyChange RegisterWorld(
            int worldId,
            int parentWorldId)
        {
            ValidateWorldId(worldId, nameof(worldId));
            ValidateWorldId(parentWorldId, nameof(parentWorldId));

            lock (topologyMutationLock)
            {
                ThrowIfNotAcceptingTopologyPublications();

                bool mappingAlreadyExists =
                    parentWorldIdsByWorldId.TryGetValue(
                        worldId,
                        out var previousParentWorldId);
                if (mappingAlreadyExists &&
                    previousParentWorldId == parentWorldId)
                {
                    // Exact repeats preserve the version and snapshot reference and
                    // allocate nothing. They are true cold-path idempotent no-ops.
                    return new WorldParentTopologyChange(
                        hasChanged: false,
                        worldId,
                        previousParentWorldId,
                        parentWorldId);
                }

                long nextVersion = GetNextVersion();
                var candidateParentWorldIdsByWorldId =
                    new Dictionary<int, int>(parentWorldIdsByWorldId)
                    {
                        [worldId] = parentWorldId,
                    };
                var candidateSnapshot = new WorldParentTopologySnapshot(
                    gameSessionGeneration,
                    new WorldParentTopologyVersion(nextVersion),
                    candidateParentWorldIdsByWorldId);

                // All allocation, grouping, and sorting completed before changing
                // catalog state. The mutable catalog map and immutable snapshot do
                // not share a dictionary, and the reference is published last.
                parentWorldIdsByWorldId =
                    candidateParentWorldIdsByWorldId;
                version = nextVersion;
                Volatile.Write(ref publishedSnapshot, candidateSnapshot);

                return new WorldParentTopologyChange(
                    hasChanged: true,
                    worldId,
                    mappingAlreadyExists
                        ? previousParentWorldId
                        : (int?)null,
                    parentWorldId);
            }
        }

        internal WorldParentTopologyChange RemoveWorld(int worldId)
        {
            ValidateWorldId(worldId, nameof(worldId));

            lock (topologyMutationLock)
            {
                ThrowIfNotAcceptingTopologyPublications();

                if (!parentWorldIdsByWorldId.TryGetValue(
                        worldId,
                        out var previousParentWorldId))
                {
                    return new WorldParentTopologyChange(
                        hasChanged: false,
                        worldId,
                        previousParentWorldId: null,
                        currentParentWorldId: null);
                }

                long nextVersion = GetNextVersion();
                var candidateParentWorldIdsByWorldId =
                    new Dictionary<int, int>(parentWorldIdsByWorldId);
                if (!candidateParentWorldIdsByWorldId.Remove(worldId))
                {
                    throw new InvalidOperationException(
                        "The candidate world-parent topology did not contain the " +
                        "mapping that the catalog had just resolved.");
                }

                var candidateSnapshot = new WorldParentTopologySnapshot(
                    gameSessionGeneration,
                    new WorldParentTopologyVersion(nextVersion),
                    candidateParentWorldIdsByWorldId);
                parentWorldIdsByWorldId =
                    candidateParentWorldIdsByWorldId;
                version = nextVersion;
                Volatile.Write(ref publishedSnapshot, candidateSnapshot);

                return new WorldParentTopologyChange(
                    hasChanged: true,
                    worldId,
                    previousParentWorldId,
                    currentParentWorldId: null);
            }
        }

        internal void ClearForGameSession()
        {
            lock (topologyMutationLock)
            {
                if (!acceptsTopologyPublications)
                {
                    return;
                }

                // Do not publish an empty shutdown snapshot. Existing readers retain
                // the last complete immutable topology, while this mutable catalog
                // releases its owned map and rejects every later publication.
                acceptsTopologyPublications = false;
                parentWorldIdsByWorldId.Clear();
            }
        }

        private long GetNextVersion()
        {
            if (version == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "The world-parent topology version is exhausted; the catalog " +
                    "will not wrap or publish a reusable version.");
            }

            try
            {
                long nextVersion = checked(version + 1);
                if (nextVersion <= 0)
                {
                    throw new InvalidOperationException(
                        "An effective world-parent topology mutation requires a " +
                        "positive version.");
                }

                return nextVersion;
            }
            catch (OverflowException exception)
            {
                throw new InvalidOperationException(
                    "The world-parent topology version is exhausted; the catalog " +
                    "will not wrap or publish a reusable version.",
                    exception);
            }
        }

        private void ThrowIfNotAcceptingTopologyPublications()
        {
            if (!acceptsTopologyPublications)
            {
                throw new InvalidOperationException(
                    "The world-parent topology catalog no longer accepts " +
                    "publications because its game session has been released.");
            }
        }

        private static void ValidateWorldId(
            int worldId,
            string parameterName)
        {
            if (worldId < 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    worldId,
                    "An ONI world identity cannot be negative.");
            }
        }
    }
}
