#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Immutable normalized delivery-temperature behavior for one destination.
    /// Serialized component fields are converted here once so every consumer uses
    /// the same inclusive-minimum, exclusive-maximum decision.
    /// </summary>
    internal readonly struct DeliveryTemperatureConstraint :
        IEquatable<DeliveryTemperatureConstraint>
    {
        private DeliveryTemperatureConstraint(
            int minimumInclusiveKelvin,
            int maximumExclusiveKelvin)
        {
            MinimumInclusiveKelvin = minimumInclusiveKelvin;
            MaximumExclusiveKelvin = maximumExclusiveKelvin;
        }

        internal int MinimumInclusiveKelvin { get; }

        internal int MaximumExclusiveKelvin { get; }

        internal bool IsEnabled => MaximumExclusiveKelvin > 0;

        // Disabled and empty are intentionally distinct states. A zero maximum
        // disables filtering and preserves ordinary ONI behavior; only an enabled
        // range whose minimum cannot precede its maximum rejects every temperature.
        internal bool IsEmpty =>
            IsEnabled && MinimumInclusiveKelvin >= MaximumExclusiveKelvin;

        internal static DeliveryTemperatureConstraint FromSerializedLimits(
            int serializedLowLimit,
            int serializedHighLimit)
        {
            // Clamp the two serialized fields independently before interpreting
            // enabled state. In particular, a negative saved high value becomes
            // zero and therefore remains the established disabled representation.
            int minimumInclusiveKelvin = ClampToStorableTemperatureRange(
                serializedLowLimit);
            int maximumExclusiveKelvin = ClampToStorableTemperatureRange(
                serializedHighLimit);
            return new DeliveryTemperatureConstraint(
                minimumInclusiveKelvin,
                maximumExclusiveKelvin);
        }

        internal bool Allows(float temperatureKelvin)
        {
            if (!IsEnabled)
            {
                return true;
            }

            // The existing mod casts before comparing. C# integer conversion
            // truncates toward zero, so moving this conversion into callers or
            // replacing it with floor/round would change negative-fraction behavior.
            int truncatedKelvin = (int)temperatureKelvin;
            return MinimumInclusiveKelvin <= truncatedKelvin &&
                truncatedKelvin < MaximumExclusiveKelvin;
        }

        public bool Equals(DeliveryTemperatureConstraint other) =>
            MinimumInclusiveKelvin == other.MinimumInclusiveKelvin &&
            MaximumExclusiveKelvin == other.MaximumExclusiveKelvin;

        public override bool Equals(object? obj) =>
            obj is DeliveryTemperatureConstraint other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (MinimumInclusiveKelvin * 397) ^ MaximumExclusiveKelvin;
            }
        }

        private static int ClampToStorableTemperatureRange(int temperatureKelvin)
        {
            if (temperatureKelvin < OniStorableTemperatureBounds.MinimumTemperatureKelvin)
            {
                return OniStorableTemperatureBounds.MinimumTemperatureKelvin;
            }

            if (temperatureKelvin > OniStorableTemperatureBounds.MaximumTemperatureKelvin)
            {
                return OniStorableTemperatureBounds.MaximumTemperatureKelvin;
            }

            return temperatureKelvin;
        }
    }
}
