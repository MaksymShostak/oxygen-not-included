#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Monotonic identity of the authoritative fetch-request topology.
    /// </summary>
    internal readonly struct FetchRequestTopologyVersion :
        IEquatable<FetchRequestTopologyVersion>
    {
        internal FetchRequestTopologyVersion(long value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "A fetch-request topology version cannot be negative.");
            }

            Value = value;
        }

        internal long Value { get; }

        public bool Equals(FetchRequestTopologyVersion other) =>
            Value == other.Value;

        public override bool Equals(object? obj) =>
            obj is FetchRequestTopologyVersion other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();
    }
}
