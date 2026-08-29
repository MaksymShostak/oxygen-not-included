#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Immutable proof that exactly one present resource tag's complete
    /// temperature amounts were refreshed for one collection generation.
    /// </summary>
    /// <remarks>
    /// This publication makes no claim about any other resource tag and must not
    /// be substituted for a complete-world inventory publication or complete key
    /// coverage.
    /// </remarks>
    internal readonly struct WorldResourceTemperatureSeriesPublication
    {
        internal WorldResourceTemperatureSeriesPublication(
            WorldInventoryCollectionGeneration collectionGeneration,
            Tag resourceTag,
            TemperatureAmountSeries temperatureAmounts)
        {
            if (temperatureAmounts == null)
            {
                throw new ArgumentNullException(nameof(temperatureAmounts));
            }

            CollectionGeneration = collectionGeneration;
            ResourceTag = resourceTag;
            TemperatureAmounts = temperatureAmounts;
        }

        internal WorldInventoryCollectionGeneration CollectionGeneration { get; }

        internal Tag ResourceTag { get; }

        internal TemperatureAmountSeries TemperatureAmounts { get; }
    }
}
