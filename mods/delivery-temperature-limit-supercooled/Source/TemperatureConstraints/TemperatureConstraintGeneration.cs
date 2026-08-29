#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Monotonic identity of the complete active-temperature-constraint state.
    /// Consumers compare this value with dependent snapshots so stale optimized
    /// data cannot be mistaken for current delivery eligibility.
    /// </summary>
    internal readonly struct TemperatureConstraintGeneration :
        IEquatable<TemperatureConstraintGeneration>
    {
        internal TemperatureConstraintGeneration(long value)
        {
            Value = value;
        }

        internal long Value { get; }

        public bool Equals(TemperatureConstraintGeneration other) =>
            Value == other.Value;

        public override bool Equals(object? obj) =>
            obj is TemperatureConstraintGeneration other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();
    }
}
