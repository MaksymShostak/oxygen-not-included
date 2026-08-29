#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Immutable complete resource-tag-to-temperature-series map for one world at
    /// one inventory collection generation.
    /// </summary>
    /// <remarks>
    /// Unlike tag coverage or a single-tag refresh, this publication proves both
    /// presence and complete temperature amounts for every key it contains, and
    /// proves absence for every key it omits.
    /// </remarks>
    internal sealed class CompleteWorldResourceTemperatureAmounts
    {
        private readonly Dictionary<Tag, TemperatureAmountSeries>
            temperatureAmountsByResourceTag;
        private readonly Tag[] resourceTags;
        private readonly ReadOnlyCollection<Tag> readOnlyResourceTags;

        private CompleteWorldResourceTemperatureAmounts(
            WorldInventoryCollectionGeneration collectionGeneration,
            Dictionary<Tag, TemperatureAmountSeries>
                temperatureAmountsByResourceTag,
            Tag[] resourceTags)
        {
            CollectionGeneration = collectionGeneration;
            this.temperatureAmountsByResourceTag =
                temperatureAmountsByResourceTag;
            this.resourceTags = resourceTags;
            readOnlyResourceTags = Array.AsReadOnly(this.resourceTags);
        }

        internal WorldInventoryCollectionGeneration CollectionGeneration { get; }

        internal IReadOnlyList<Tag> ResourceTags => readOnlyResourceTags;

        /// <summary>
        /// Copies a completed candidate so subsequent builder reuse cannot mutate
        /// the published world state.
        /// </summary>
        internal static CompleteWorldResourceTemperatureAmounts Create(
            WorldInventoryCollectionGeneration collectionGeneration,
            IReadOnlyDictionary<Tag, TemperatureAmountSeries>
                sourceTemperatureAmountsByResourceTag)
        {
            if (sourceTemperatureAmountsByResourceTag == null)
            {
                throw new ArgumentNullException(
                    nameof(sourceTemperatureAmountsByResourceTag));
            }

            var ownedTemperatureAmountsByResourceTag =
                new Dictionary<Tag, TemperatureAmountSeries>(
                    sourceTemperatureAmountsByResourceTag.Count);
            var ownedResourceTags =
                new Tag[sourceTemperatureAmountsByResourceTag.Count];
            int resourceTagIndex = 0;
            foreach (KeyValuePair<Tag, TemperatureAmountSeries> entry in
                sourceTemperatureAmountsByResourceTag)
            {
                if (entry.Value == null)
                {
                    throw new ArgumentException(
                        "A complete world publication cannot contain a null " +
                        "temperature amount series.",
                        nameof(sourceTemperatureAmountsByResourceTag));
                }

                ownedTemperatureAmountsByResourceTag.Add(
                    entry.Key,
                    entry.Value);
                ownedResourceTags[resourceTagIndex] = entry.Key;
                resourceTagIndex++;
            }

            return new CompleteWorldResourceTemperatureAmounts(
                collectionGeneration,
                ownedTemperatureAmountsByResourceTag,
                ownedResourceTags);
        }

        internal bool TryGetSeries(
            Tag resourceTag,
            out TemperatureAmountSeries series)
        {
            TemperatureAmountSeries? foundSeries;
            if (temperatureAmountsByResourceTag.TryGetValue(
                resourceTag,
                out foundSeries))
            {
                series = foundSeries;
                return true;
            }

            // A non-null deterministic out value simplifies conservative callers;
            // the false return remains the sole proof that this complete map
            // omitted the resource tag.
            series = TemperatureAmountSeries.Empty;
            return false;
        }
    }
}
