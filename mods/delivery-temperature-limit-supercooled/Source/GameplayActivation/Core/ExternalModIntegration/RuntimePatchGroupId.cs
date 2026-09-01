#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Provider-neutral audit identity of one concrete prepared runtime patch
    /// group. It identifies registration responsibility, not capability owner.
    /// </summary>
    internal readonly struct RuntimePatchGroupId :
        IEquatable<RuntimePatchGroupId>
    {
        internal RuntimePatchGroupId(string value)
        {
            Value = ValidatedIntegrationIdentifier.RequireKebabCase(
                value,
                nameof(value));
        }

        internal string Value { get; }

        public bool Equals(RuntimePatchGroupId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object? obj) =>
            obj is RuntimePatchGroupId other && Equals(other);

        public override int GetHashCode() =>
            Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value ?? string.Empty;
    }
}
