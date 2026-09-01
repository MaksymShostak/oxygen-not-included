#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Stable identity of one semantic runtime capability whose implementation
    /// may be supplied by Klei or by one declared external integration.
    /// </summary>
    internal readonly struct RuntimeCapabilityId :
        IEquatable<RuntimeCapabilityId>
    {
        internal static readonly RuntimeCapabilityId
            WorldInventoryTemperaturePublication =
                new RuntimeCapabilityId(
                    "world-inventory-temperature-publication");

        internal static readonly RuntimeCapabilityId PickupTemperatureGrouping =
            new RuntimeCapabilityId("pickup-temperature-grouping");

        internal static readonly RuntimeCapabilityId DirectDeliveryEligibility =
            new RuntimeCapabilityId("direct-delivery-eligibility");

        internal static readonly RuntimeCapabilityId
            TemperatureStatusAvailability =
                new RuntimeCapabilityId("temperature-status-availability");

        internal RuntimeCapabilityId(string value)
        {
            Value = ValidatedIntegrationIdentifier.RequireKebabCase(
                value,
                nameof(value));
        }

        internal string Value { get; }

        public bool Equals(RuntimeCapabilityId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object? obj) =>
            obj is RuntimeCapabilityId other && Equals(other);

        public override int GetHashCode() =>
            Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value ?? string.Empty;
    }
}
