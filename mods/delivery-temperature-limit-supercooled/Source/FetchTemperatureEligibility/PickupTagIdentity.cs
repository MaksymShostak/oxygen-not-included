#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Complete pickup tag-grouping identity inherited from ONI's pickup path.
    /// </summary>
    internal readonly struct PickupTagIdentity : IEquatable<PickupTagIdentity>
    {
        internal PickupTagIdentity(int originalTagBitsHash, Tag prefabTag)
        {
            OriginalTagBitsHash = originalTagBitsHash;
            PrefabTag = prefabTag;
        }

        internal int OriginalTagBitsHash { get; }

        internal Tag PrefabTag { get; }

        public bool Equals(PickupTagIdentity other) =>
            OriginalTagBitsHash == other.OriginalTagBitsHash &&
            PrefabTag.Equals(other.PrefabTag);

        public override bool Equals(object? obj) =>
            obj is PickupTagIdentity other && Equals(other);

        public override int GetHashCode()
        {
            // Hash collisions remain legal because dictionary equality always checks
            // both fields. In particular, the original tag-bits hash alone is not
            // treated as an identity when different prefab tags share that value.
            unchecked
            {
                return (OriginalTagBitsHash * 397) ^ PrefabTag.GetHashCode();
            }
        }
    }
}
