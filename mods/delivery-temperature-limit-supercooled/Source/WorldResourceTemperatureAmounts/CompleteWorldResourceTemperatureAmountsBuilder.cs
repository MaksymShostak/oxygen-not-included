#nullable enable

using System;
using System.Collections.Generic;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Reusable, explicitly sequenced builder for one complete-world resource
    /// temperature publication.
    /// </summary>
    internal sealed class CompleteWorldResourceTemperatureAmountsBuilder
    {
        private enum BuilderState
        {
            Idle,
            BuildingWorld,
            BuildingResourceTag,
            Completed
        }

        // The exact field name is an intentional retention-policy contract. Tests
        // verify that an unusually large world cannot pin its peak dictionary
        // capacity for the remainder of the game session.
        private Dictionary<Tag, TemperatureAmountSeries>
            temperatureAmountsByResourceTag =
                new Dictionary<Tag, TemperatureAmountSeries>();
        private TemperatureAmountAccumulator temperatureAmountAccumulator =
            new TemperatureAmountAccumulator();

        private BuilderState state;
        private WorldInventoryCollectionGeneration collectionGeneration;
        private Tag openResourceTag;

        internal void BeginWorld(
            WorldInventoryCollectionGeneration collectionGeneration)
        {
            if (state == BuilderState.BuildingWorld ||
                state == BuilderState.BuildingResourceTag)
            {
                throw new InvalidOperationException(
                    "The builder is already building a world inventory. " +
                    "Build or discard it before beginning another.");
            }

            // Build and Discard always release the previous logical contents.
            // This guard makes any future state-machine regression fail closed.
            if (temperatureAmountsByResourceTag.Count != 0)
            {
                throw new InvalidOperationException(
                    "The builder retained candidate entries outside a build " +
                    "lifecycle and cannot safely begin another world.");
            }

            this.collectionGeneration = collectionGeneration;
            openResourceTag = default(Tag);
            state = BuilderState.BuildingWorld;
        }

        internal void BeginResourceTag(Tag resourceTag)
        {
            if (state == BuilderState.BuildingResourceTag)
            {
                throw new InvalidOperationException(
                    "A resource tag is already open. CompleteResourceTag must " +
                    "finish it before another resource tag begins.");
            }

            if (state != BuilderState.BuildingWorld)
            {
                throw new InvalidOperationException(
                    "BeginWorld must start a complete-world build before " +
                    "BeginResourceTag can be called.");
            }

            if (temperatureAmountsByResourceTag.ContainsKey(resourceTag))
            {
                throw new InvalidOperationException(
                    "This resource tag has already been completed in the " +
                    "current complete-world candidate.");
            }

            temperatureAmountAccumulator.BeginResourceTag();
            openResourceTag = resourceTag;
            state = BuilderState.BuildingResourceTag;
        }

        internal void AddTemperatureAmount(
            float temperatureKelvin,
            float amount)
        {
            if (state != BuilderState.BuildingResourceTag)
            {
                throw new InvalidOperationException(
                    "BeginResourceTag must open a resource tag before " +
                    "temperature amounts can be added.");
            }

            temperatureAmountAccumulator.AddTemperatureAmount(
                temperatureKelvin,
                amount);
        }

        internal void CompleteResourceTag()
        {
            if (state != BuilderState.BuildingResourceTag)
            {
                throw new InvalidOperationException(
                    "BeginResourceTag must open a resource tag before " +
                    "CompleteResourceTag can be called.");
            }

            TemperatureAmountSeries temperatureAmounts =
                temperatureAmountAccumulator.BuildSeries();
            temperatureAmountsByResourceTag.Add(
                openResourceTag,
                temperatureAmounts);
            openResourceTag = default(Tag);
            state = BuilderState.BuildingWorld;
        }

        internal CompleteWorldResourceTemperatureAmounts Build()
        {
            if (state == BuilderState.BuildingResourceTag)
            {
                throw new InvalidOperationException(
                    "CompleteResourceTag must close the open resource tag before " +
                    "the complete-world publication can be built.");
            }

            if (state == BuilderState.Completed)
            {
                throw new InvalidOperationException(
                    "The current complete-world candidate has already been built.");
            }

            if (state != BuilderState.BuildingWorld)
            {
                throw new InvalidOperationException(
                    "BeginWorld must start a complete-world build before Build " +
                    "can be called.");
            }

            CompleteWorldResourceTemperatureAmounts publication =
                CompleteWorldResourceTemperatureAmounts.Create(
                    collectionGeneration,
                    temperatureAmountsByResourceTag);

            ReleaseCandidateMap();
            collectionGeneration = default(WorldInventoryCollectionGeneration);
            openResourceTag = default(Tag);
            state = BuilderState.Completed;
            return publication;
        }

        /// <summary>
        /// Abandons any partial candidate and releases every candidate reference.
        /// This method is safe to call from adapter exception paths in any state.
        /// </summary>
        internal void Discard()
        {
            if (state == BuilderState.BuildingResourceTag)
            {
                // TemperatureAmountAccumulator intentionally exposes no reset
                // shim. Replacing this one bounded scratch object is the rare
                // exception-path operation that safely abandons its open tag.
                temperatureAmountAccumulator =
                    new TemperatureAmountAccumulator();
            }

            ReleaseCandidateMap();
            collectionGeneration = default(WorldInventoryCollectionGeneration);
            openResourceTag = default(Tag);
            state = BuilderState.Idle;
        }

        private void ReleaseCandidateMap()
        {
            if (temperatureAmountsByResourceTag.Count >
                RetainedCollectionCapacityLimits
                    .MaximumRetainedWorldResourceTagCount)
            {
                // Dictionary.Clear retains its bucket arrays. Replace an
                // exceptional peak only after every entry has already been copied
                // into a publication or deliberately discarded.
                temperatureAmountsByResourceTag =
                    new Dictionary<Tag, TemperatureAmountSeries>();
                return;
            }

            temperatureAmountsByResourceTag.Clear();
        }
    }
}
