#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Monotonic nonzero identity of one temperature-inventory collection epoch.
    /// Publications from different epochs must never be combined as if current.
    /// </summary>
    internal readonly struct WorldInventoryCollectionGeneration :
        IEquatable<WorldInventoryCollectionGeneration>
    {
        internal WorldInventoryCollectionGeneration(long value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "A world-inventory collection generation must be positive.");
            }

            Value = value;
        }

        internal long Value { get; }

        public bool Equals(WorldInventoryCollectionGeneration other) =>
            Value == other.Value;

        public override bool Equals(object? obj) =>
            obj is WorldInventoryCollectionGeneration other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();
    }
}
