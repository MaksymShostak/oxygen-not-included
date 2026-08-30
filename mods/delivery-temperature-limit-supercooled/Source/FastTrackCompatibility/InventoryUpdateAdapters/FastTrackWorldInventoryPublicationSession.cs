#nullable enable

using System;
using System.Collections.Generic;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Owns reusable pure accumulation state for one instrumented FastTrack
    /// <c>BackgroundWorldInventory.RunUpdate</c> invocation.
    /// </summary>
    /// <remarks>
    /// Complete and incremental modes are intentionally separate entry points.
    /// FastTrack's first branch proves every inventory key and may publish absence;
    /// a later branch proves amounts for only one selected key and must never be
    /// mistaken for a complete-world candidate.
    /// </remarks>
    internal sealed class FastTrackWorldInventoryPublicationSession
    {
        private enum PublicationSessionState
        {
            Inactive,
            CompleteWorldUpdate,
            IncrementalResourceTagUpdateRequiringCoverage,
            IncrementalResourceTagUpdateWithCurrentCoverage
        }

        // Keep this builder lazy. Incremental FastTrack updates are the ordinary
        // steady-state path and must not allocate a complete-world dictionary.
        private CompleteWorldResourceTemperatureAmountsBuilder?
            completeWorldResourceTemperatureAmountsBuilder;
        private TemperatureAmountAccumulator temperatureAmountAccumulator =
            new TemperatureAmountAccumulator();
        private WorldResourceTagCoverage? resourceTagCoverage;
        private WorldResourceTemperatureSeriesPublication?
            resourceTemperatureSeriesPublication;

        private PublicationSessionState state;
        private GameSessionGeneration gameSessionGeneration;
        private WorldInventoryCollectionGeneration collectionGeneration;
        private Tag openResourceTag;
        private bool resourceTagIsOpen;
        private bool incrementalResourceTagHasCompleted;

        internal void BeginCompleteWorldUpdate(
            GameSessionGeneration gameSessionGeneration,
            WorldInventoryCollectionGeneration collectionGeneration)
        {
            PrepareForBegin(gameSessionGeneration);

            CompleteWorldResourceTemperatureAmountsBuilder builder =
                completeWorldResourceTemperatureAmountsBuilder ??
                (completeWorldResourceTemperatureAmountsBuilder =
                    new CompleteWorldResourceTemperatureAmountsBuilder());
            builder.BeginWorld(collectionGeneration);
            this.gameSessionGeneration = gameSessionGeneration;
            this.collectionGeneration = collectionGeneration;
            state = PublicationSessionState.CompleteWorldUpdate;
        }

        internal void BeginIncrementalResourceTagUpdateRequiringCoverage(
            GameSessionGeneration gameSessionGeneration,
            WorldInventoryCollectionGeneration collectionGeneration,
            IEnumerable<Tag> presentResourceTags)
        {
            if (presentResourceTags == null)
            {
                throw new ArgumentNullException(nameof(presentResourceTags));
            }

            PrepareForBegin(gameSessionGeneration);

            // WorldResourceTagCoverage owns the one defensive copy. The adapter
            // passes WorldInventory.Inventory.Keys directly, so this is the sole
            // enumeration of the dictionary's key collection for a generation.
            WorldResourceTagCoverage candidateCoverage =
                WorldResourceTagCoverage.Create(
                    collectionGeneration,
                    presentResourceTags);
            this.gameSessionGeneration = gameSessionGeneration;
            this.collectionGeneration = collectionGeneration;
            resourceTagCoverage = candidateCoverage;
            state = PublicationSessionState
                .IncrementalResourceTagUpdateRequiringCoverage;
        }

        internal void BeginIncrementalResourceTagUpdateWithCurrentCoverage(
            GameSessionGeneration gameSessionGeneration,
            WorldInventoryCollectionGeneration collectionGeneration)
        {
            PrepareForBegin(gameSessionGeneration);

            this.gameSessionGeneration = gameSessionGeneration;
            this.collectionGeneration = collectionGeneration;
            state = PublicationSessionState
                .IncrementalResourceTagUpdateWithCurrentCoverage;
        }

        internal void BeginResourceTag(Tag resourceTag)
        {
            ThrowIfInactive();
            if (resourceTagIsOpen)
            {
                throw new InvalidOperationException(
                    "A FastTrack world-inventory resource tag is already open. " +
                    "CompleteResourceTag must finish it before another begins.");
            }

            if (IsIncrementalState(state) &&
                incrementalResourceTagHasCompleted)
            {
                throw new InvalidOperationException(
                    "A FastTrack incremental world-inventory update may publish " +
                    "exactly one resource tag.");
            }

            if (state == PublicationSessionState.CompleteWorldUpdate)
            {
                completeWorldResourceTemperatureAmountsBuilder!
                    .BeginResourceTag(resourceTag);
            }
            else
            {
                temperatureAmountAccumulator.BeginResourceTag();
            }

            openResourceTag = resourceTag;
            resourceTagIsOpen = true;
        }

        internal void AddTemperatureAmount(
            float temperatureKelvin,
            float amount)
        {
            if (!resourceTagIsOpen)
            {
                throw new InvalidOperationException(
                    "BeginResourceTag must open a FastTrack world-inventory " +
                    "resource tag before temperature amounts can be added.");
            }

            if (state == PublicationSessionState.CompleteWorldUpdate)
            {
                completeWorldResourceTemperatureAmountsBuilder!
                    .AddTemperatureAmount(temperatureKelvin, amount);
            }
            else
            {
                temperatureAmountAccumulator.AddTemperatureAmount(
                    temperatureKelvin,
                    amount);
            }
        }

        internal void CompleteResourceTag()
        {
            if (!resourceTagIsOpen)
            {
                throw new InvalidOperationException(
                    "BeginResourceTag must open a FastTrack world-inventory " +
                    "resource tag before CompleteResourceTag can be called.");
            }

            if (state == PublicationSessionState.CompleteWorldUpdate)
            {
                completeWorldResourceTemperatureAmountsBuilder!
                    .CompleteResourceTag();
            }
            else
            {
                TemperatureAmountSeries temperatureAmounts =
                    temperatureAmountAccumulator.BuildSeries();
                resourceTemperatureSeriesPublication =
                    new WorldResourceTemperatureSeriesPublication(
                        collectionGeneration,
                        openResourceTag,
                        temperatureAmounts);
                incrementalResourceTagHasCompleted = true;
            }

            openResourceTag = default(Tag);
            resourceTagIsOpen = false;
        }

        internal FastTrackWorldInventoryPublicationResult Complete()
        {
            ThrowIfInactive();
            if (resourceTagIsOpen)
            {
                throw new InvalidOperationException(
                    "CompleteResourceTag must close the open FastTrack " +
                    "world-inventory resource tag before publication.");
            }

            FastTrackWorldInventoryPublicationResult result;
            switch (state)
            {
                case PublicationSessionState.CompleteWorldUpdate:
                    result = FastTrackWorldInventoryPublicationResult
                        .ForCompleteWorldAmounts(
                            completeWorldResourceTemperatureAmountsBuilder!
                                .Build());
                    break;

                case PublicationSessionState
                    .IncrementalResourceTagUpdateRequiringCoverage:
                    if (resourceTemperatureSeriesPublication.HasValue)
                    {
                        result = FastTrackWorldInventoryPublicationResult
                            .ForResourceTagCoverageAndTemperatureSeries(
                                resourceTagCoverage!,
                                resourceTemperatureSeriesPublication.Value);
                    }
                    else
                    {
                        // Coverage alone is useful for an empty inventory: it
                        // proves every tag absent. For a present tag, coverage is
                        // deliberately not treated as refreshed temperature data.
                        result = FastTrackWorldInventoryPublicationResult
                            .ForResourceTagCoverageOnly(resourceTagCoverage!);
                    }

                    break;

                case PublicationSessionState
                    .IncrementalResourceTagUpdateWithCurrentCoverage:
                    if (!resourceTemperatureSeriesPublication.HasValue)
                    {
                        throw new InvalidOperationException(
                            "A FastTrack incremental update with current coverage " +
                            "must complete exactly one resource tag before " +
                            "publication.");
                    }

                    result = FastTrackWorldInventoryPublicationResult
                        .ForResourceTemperatureSeries(
                            resourceTemperatureSeriesPublication.Value);
                    break;

                default:
                    throw new InvalidOperationException(
                        "An inactive FastTrack world-inventory publication " +
                        "session cannot complete.");
            }

            ReleaseLogicalState();
            return result;
        }

        /// <summary>
        /// Abandons an incomplete invocation and releases every candidate-owned
        /// reference. It is safe for finalizers to call this method repeatedly.
        /// </summary>
        internal void Discard()
        {
            if (state == PublicationSessionState.CompleteWorldUpdate &&
                completeWorldResourceTemperatureAmountsBuilder != null)
            {
                completeWorldResourceTemperatureAmountsBuilder.Discard();
            }
            else if (IsIncrementalState(state) && resourceTagIsOpen)
            {
                // TemperatureAmountAccumulator exposes no reset operation by
                // design. Replacing this bounded scratch object is the safe rare
                // exception path that closes its open resource-tag lifecycle.
                temperatureAmountAccumulator =
                    new TemperatureAmountAccumulator();
            }

            ReleaseLogicalState();
        }

        private void PrepareForBegin(
            GameSessionGeneration requestedGameSessionGeneration)
        {
            if (state == PublicationSessionState.Inactive)
            {
                return;
            }

            if (gameSessionGeneration.Equals(requestedGameSessionGeneration))
            {
                throw new InvalidOperationException(
                    "A FastTrack world-inventory publication session is already " +
                    "active for this game-session generation.");
            }

            // A delayed finalizer from a prior save must not pin or contaminate
            // state used by a newer game-session composition root.
            Discard();
        }

        private void ThrowIfInactive()
        {
            if (state == PublicationSessionState.Inactive)
            {
                throw new InvalidOperationException(
                    "A FastTrack world-inventory update must begin before a " +
                    "resource tag can be accumulated or published.");
            }
        }

        private void ReleaseLogicalState()
        {
            resourceTagCoverage = null;
            resourceTemperatureSeriesPublication = null;
            state = PublicationSessionState.Inactive;
            gameSessionGeneration = default(GameSessionGeneration);
            collectionGeneration =
                default(WorldInventoryCollectionGeneration);
            openResourceTag = default(Tag);
            resourceTagIsOpen = false;
            incrementalResourceTagHasCompleted = false;
        }

        private static bool IsIncrementalState(
            PublicationSessionState candidateState) =>
            candidateState == PublicationSessionState
                .IncrementalResourceTagUpdateRequiringCoverage ||
            candidateState == PublicationSessionState
                .IncrementalResourceTagUpdateWithCurrentCoverage;
    }
}
