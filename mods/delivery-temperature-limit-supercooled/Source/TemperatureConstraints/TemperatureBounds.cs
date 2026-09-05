#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Immutable optional temperature boundaries for presentation and filtering.
    /// Encapsulates the conversion between high-level optional bounds (null meaning
    /// unbounded) and the low-level serialized Kelvin / disabled sentinel model.
    /// </summary>
    internal readonly struct TemperatureBounds : IEquatable<TemperatureBounds>
    {
        public static readonly TemperatureBounds Unbounded = new TemperatureBounds(null, null);

        public TemperatureBounds(int? lowerKelvin, int? upperKelvin)
        {
            LowerKelvin = lowerKelvin;
            UpperKelvin = upperKelvin;
        }

        public int? LowerKelvin { get; }

        public int? UpperKelvin { get; }

        public bool IsUnbounded => !LowerKelvin.HasValue && !UpperKelvin.HasValue;

        public bool IsEmpty =>
            LowerKelvin.HasValue &&
            UpperKelvin.HasValue &&
            LowerKelvin.Value >= UpperKelvin.Value;

        public bool IsEqualBounds =>
            LowerKelvin.HasValue &&
            UpperKelvin.HasValue &&
            LowerKelvin.Value == UpperKelvin.Value;

        public static TemperatureBounds FromConstraint(DeliveryTemperatureConstraint constraint)
        {
            if (!constraint.IsEnabled)
            {
                return Unbounded;
            }

            int? lower = constraint.MinimumInclusiveKelvin <= OniStorableTemperatureBounds.MinimumTemperatureKelvin
                ? (int?)null
                : constraint.MinimumInclusiveKelvin;
            int? upper = constraint.MaximumExclusiveKelvin >= OniStorableTemperatureBounds.MaximumTemperatureKelvin
                ? (int?)null
                : constraint.MaximumExclusiveKelvin;
            return new TemperatureBounds(lower, upper);
        }

        public DeliveryTemperatureConstraint ToConstraint()
        {
            if (IsUnbounded)
            {
                return DeliveryTemperatureConstraint.FromSerializedLimits(
                    OniStorableTemperatureBounds.MinimumTemperatureKelvin,
                    0);
            }

            int lower = LowerKelvin ?? OniStorableTemperatureBounds.MinimumTemperatureKelvin;
            int upper = UpperKelvin ?? OniStorableTemperatureBounds.MaximumTemperatureKelvin;
            return DeliveryTemperatureConstraint.FromSerializedLimits(lower, upper);
        }

        public (int SerializedLow, int SerializedHigh) ToSerializedLimits()
        {
            if (IsUnbounded)
            {
                return (OniStorableTemperatureBounds.MinimumTemperatureKelvin, 0);
            }

            int lower = LowerKelvin ?? OniStorableTemperatureBounds.MinimumTemperatureKelvin;
            int upper = UpperKelvin ?? OniStorableTemperatureBounds.MaximumTemperatureKelvin;
            return (lower, upper);
        }

        public bool Equals(TemperatureBounds other) =>
            Nullable.Equals(LowerKelvin, other.LowerKelvin) &&
            Nullable.Equals(UpperKelvin, other.UpperKelvin);

        public override bool Equals(object? obj) =>
            obj is TemperatureBounds other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((LowerKelvin?.GetHashCode() ?? 0) * 397) ^
                    (UpperKelvin?.GetHashCode() ?? 0);
            }
        }

        public override string ToString()
        {
            if (IsUnbounded)
            {
                return "Unbounded";
            }

            string low = LowerKelvin.HasValue ? $"{LowerKelvin.Value} K" : "none";
            string high = UpperKelvin.HasValue ? $"{UpperKelvin.Value} K" : "none";
            return $"[{low}, {high})";
        }
    }
}
