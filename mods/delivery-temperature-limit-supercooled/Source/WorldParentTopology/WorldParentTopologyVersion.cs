#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Monotonic identity of one complete immutable world-parent mapping.
    /// Version zero belongs only to the initial empty topology; every effective
    /// catalog mutation publishes the next positive version exactly once.
    /// </summary>
    internal readonly struct WorldParentTopologyVersion :
        IEquatable<WorldParentTopologyVersion>
    {
        internal WorldParentTopologyVersion(long value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "A world-parent topology version cannot be negative.");
            }

            Value = value;
        }

        internal long Value { get; }

        public bool Equals(WorldParentTopologyVersion other) =>
            Value == other.Value;

        public override bool Equals(object? obj) =>
            obj is WorldParentTopologyVersion other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();
    }
}
