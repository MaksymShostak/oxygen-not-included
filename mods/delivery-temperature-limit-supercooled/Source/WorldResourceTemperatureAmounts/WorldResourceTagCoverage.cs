#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Immutable proof of every resource-tag key present during one complete key
    /// enumeration for one world and collection generation.
    /// </summary>
    /// <remarks>
    /// Coverage proves only that a tag key was present or absent. It does not
    /// prove that a present tag's temperature amounts were refreshed; only a
    /// <see cref="WorldResourceTemperatureSeriesPublication"/> supplies that
    /// distinct proof.
    /// </remarks>
    internal sealed class WorldResourceTagCoverage
    {
        private readonly Tag[] presentResourceTags;
        private readonly HashSet<Tag> presentResourceTagMembership;
        private readonly ReadOnlyCollection<Tag> readOnlyPresentResourceTags;

        private WorldResourceTagCoverage(
            WorldInventoryCollectionGeneration collectionGeneration,
            Tag[] presentResourceTags,
            HashSet<Tag> presentResourceTagMembership)
        {
            CollectionGeneration = collectionGeneration;
            this.presentResourceTags = presentResourceTags;
            this.presentResourceTagMembership = presentResourceTagMembership;
            readOnlyPresentResourceTags =
                Array.AsReadOnly(this.presentResourceTags);
        }

        internal WorldInventoryCollectionGeneration CollectionGeneration { get; }

        internal IReadOnlyList<Tag> PresentResourceTags =>
            readOnlyPresentResourceTags;

        internal static WorldResourceTagCoverage Create(
            WorldInventoryCollectionGeneration collectionGeneration,
            IReadOnlyCollection<Tag> presentResourceTags)
        {
            if (presentResourceTags == null)
            {
                throw new ArgumentNullException(nameof(presentResourceTags));
            }

            // Preserve first-seen order for diagnostics and deterministic tests.
            // ONI dictionary enumeration is not assumed to be alphabetical.
            var uniquePresentResourceTags =
                new List<Tag>(presentResourceTags.Count);
            var presentResourceTagMembership =
                new HashSet<Tag>();
            foreach (Tag presentResourceTag in presentResourceTags)
            {
                if (presentResourceTagMembership.Add(presentResourceTag))
                {
                    uniquePresentResourceTags.Add(presentResourceTag);
                }
            }

            return new WorldResourceTagCoverage(
                collectionGeneration,
                uniquePresentResourceTags.ToArray(),
                presentResourceTagMembership);
        }

        /// <summary>
        /// Tests membership without allocating or exposing the owned set.
        /// </summary>
        internal bool Contains(Tag resourceTag) =>
            presentResourceTagMembership.Contains(resourceTag);
    }
}
