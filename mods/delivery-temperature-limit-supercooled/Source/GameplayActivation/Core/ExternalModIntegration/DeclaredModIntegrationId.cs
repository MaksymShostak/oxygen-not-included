#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Stable compile-time identity of one declared external-mod integration.
    /// </summary>
    internal readonly struct DeclaredModIntegrationId :
        IEquatable<DeclaredModIntegrationId>
    {
        internal DeclaredModIntegrationId(string value)
        {
            Value = ValidatedIntegrationIdentifier.RequireKebabCase(
                value,
                nameof(value));
        }

        internal string Value { get; }

        public bool Equals(DeclaredModIntegrationId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object? obj) =>
            obj is DeclaredModIntegrationId other && Equals(other);

        public override int GetHashCode() =>
            Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value ?? string.Empty;
    }
}
