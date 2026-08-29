#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Ownership identity for one component's current registry entry.
    /// A replacement receives a new nonzero sequence so delayed cleanup carrying
    /// an older token cannot remove or modify the replacement.
    /// </summary>
    internal readonly struct TemperatureConstraintRegistrationToken :
        IEquatable<TemperatureConstraintRegistrationToken>
    {
        internal TemperatureConstraintRegistrationToken(
            int componentInstanceId,
            long registrationSequence)
        {
            ComponentInstanceId = componentInstanceId;
            RegistrationSequence = registrationSequence;
        }

        internal int ComponentInstanceId { get; }

        internal long RegistrationSequence { get; }

        public bool Equals(TemperatureConstraintRegistrationToken other) =>
            ComponentInstanceId == other.ComponentInstanceId &&
            RegistrationSequence == other.RegistrationSequence;

        public override bool Equals(object? obj) =>
            obj is TemperatureConstraintRegistrationToken other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (ComponentInstanceId * 397) ^ RegistrationSequence.GetHashCode();
            }
        }
    }
}
