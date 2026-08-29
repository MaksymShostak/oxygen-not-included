#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// One normalized inclusive-minimum, exclusive-maximum integer-Kelvin
    /// interval admitted by at least one constrained destination.
    /// </summary>
    internal readonly struct AllowedTemperatureInterval :
        IEquatable<AllowedTemperatureInterval>
    {
        internal AllowedTemperatureInterval(
            int minimumInclusiveKelvin,
            int maximumExclusiveKelvin)
        {
            if (minimumInclusiveKelvin <
                    OniStorableTemperatureBounds.MinimumTemperatureKelvin ||
                minimumInclusiveKelvin >=
                    OniStorableTemperatureBounds.MaximumTemperatureKelvin)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumInclusiveKelvin),
                    minimumInclusiveKelvin,
                    "An allowed interval minimum must identify an ordinary " +
                    "storable integer-Kelvin decision bucket.");
            }

            if (maximumExclusiveKelvin <= minimumInclusiveKelvin ||
                maximumExclusiveKelvin >
                    OniStorableTemperatureBounds.MaximumTemperatureKelvin)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumExclusiveKelvin),
                    maximumExclusiveKelvin,
                    "An allowed interval maximum must follow its minimum and " +
                    "remain within ONI's storable-temperature bound.");
            }

            MinimumInclusiveKelvin = minimumInclusiveKelvin;
            MaximumExclusiveKelvin = maximumExclusiveKelvin;
        }

        internal int MinimumInclusiveKelvin { get; }

        internal int MaximumExclusiveKelvin { get; }

        public bool Equals(AllowedTemperatureInterval other) =>
            MinimumInclusiveKelvin == other.MinimumInclusiveKelvin &&
            MaximumExclusiveKelvin == other.MaximumExclusiveKelvin;

        public override bool Equals(object? obj) =>
            obj is AllowedTemperatureInterval other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (MinimumInclusiveKelvin * 397) ^
                    MaximumExclusiveKelvin;
            }
        }
    }
}
