#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Canonical behaviorally significant integer-temperature classification.
    /// Sentinels collapse only values that every valid enabled constraint treats
    /// identically; each configurable integer Kelvin value keeps its own bucket.
    /// </summary>
    internal readonly struct TemperatureDecisionBucket :
        IEquatable<TemperatureDecisionBucket>,
        IComparable<TemperatureDecisionBucket>
    {
        internal const int BucketCount =
            1 + OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1;
        internal const int BelowMinimumKelvinOrdinal = 0;
        internal const int FirstIntegerKelvinOrdinal = BelowMinimumKelvinOrdinal + 1;
        internal const int AtOrAboveMaximumKelvinOrdinal = BucketCount - 1;
        internal const int HighestIntegerKelvinOrdinal =
            AtOrAboveMaximumKelvinOrdinal - 1;

        private TemperatureDecisionBucket(int ordinal)
        {
            Ordinal = ordinal;
        }

        internal int Ordinal { get; }

        internal bool IsBelowMinimumKelvin => Ordinal == BelowMinimumKelvinOrdinal;

        internal bool IsAtOrAboveMaximumKelvin =>
            Ordinal == AtOrAboveMaximumKelvinOrdinal;

        internal bool TryGetIntegerKelvin(out int integerKelvin)
        {
            if (Ordinal >= FirstIntegerKelvinOrdinal &&
                Ordinal <= HighestIntegerKelvinOrdinal)
            {
                integerKelvin = Ordinal - FirstIntegerKelvinOrdinal;
                return true;
            }

            integerKelvin = 0;
            return false;
        }

        internal static TemperatureDecisionBucket FromTemperature(float temperatureKelvin)
        {
            // Preserve the same C# truncation used by DeliveryTemperatureConstraint.
            // For example, -0.999 K truncates to 0 K rather than entering the
            // below-minimum sentinel. Ordinary negative Celsius values are still
            // positive Kelvin values and therefore use ordinary integer buckets.
            int truncatedKelvin = (int)temperatureKelvin;
            return FromIntegerKelvin(truncatedKelvin);
        }

        internal static TemperatureDecisionBucket FromIntegerKelvin(int truncatedKelvin)
        {
            if (truncatedKelvin < OniStorableTemperatureBounds.MinimumTemperatureKelvin)
            {
                return new TemperatureDecisionBucket(BelowMinimumKelvinOrdinal);
            }

            if (truncatedKelvin >= OniStorableTemperatureBounds.MaximumTemperatureKelvin)
            {
                // Exactly 10000 K is valid ONI state, but the mod's greatest
                // configurable maximum is exclusive. Every enabled constraint
                // therefore treats it as rejection-equivalent to greater values.
                return new TemperatureDecisionBucket(AtOrAboveMaximumKelvinOrdinal);
            }

            return new TemperatureDecisionBucket(
                FirstIntegerKelvinOrdinal + truncatedKelvin);
        }

        public bool Equals(TemperatureDecisionBucket other) =>
            Ordinal == other.Ordinal;

        public override bool Equals(object? obj) =>
            obj is TemperatureDecisionBucket other && Equals(other);

        public override int GetHashCode() => Ordinal;

        public int CompareTo(TemperatureDecisionBucket other) =>
            Ordinal.CompareTo(other.Ordinal);
    }
}
