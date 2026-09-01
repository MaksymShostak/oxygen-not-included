#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    internal enum RuntimeAuthorityImplementationKind
    {
        KleiBaseline = 1,
        DeclaredExternalIntegration = 2
    }

    /// <summary>
    /// Identifies the implementation family behind one prepared runtime-
    /// authority contribution. The origin discriminator keeps Temperature
    /// Limit's Klei baseline distinct from an external integration even when
    /// their textual identifiers happen to be equal.
    /// </summary>
    internal readonly struct RuntimeAuthorityImplementationIdentity :
        IEquatable<RuntimeAuthorityImplementationIdentity>
    {
        private RuntimeAuthorityImplementationIdentity(
            RuntimeAuthorityImplementationKind kind,
            DeclaredModIntegrationId? declaredExternalIntegrationId)
        {
            Kind = kind;
            DeclaredExternalIntegrationId = declaredExternalIntegrationId;
        }

        internal static RuntimeAuthorityImplementationIdentity KleiBaseline =>
            new RuntimeAuthorityImplementationIdentity(
                RuntimeAuthorityImplementationKind.KleiBaseline,
                null);

        internal static RuntimeAuthorityImplementationIdentity
            ForDeclaredExternalIntegration(
                DeclaredModIntegrationId integrationId)
        {
            ExternalModIntegrationModelValidation.RequireIntegrationId(
                integrationId,
                nameof(integrationId));
            return new RuntimeAuthorityImplementationIdentity(
                RuntimeAuthorityImplementationKind
                    .DeclaredExternalIntegration,
                integrationId);
        }

        internal RuntimeAuthorityImplementationKind Kind { get; }

        internal DeclaredModIntegrationId? DeclaredExternalIntegrationId
        {
            get;
        }

        internal bool IsKleiBaseline =>
            Kind == RuntimeAuthorityImplementationKind.KleiBaseline;

        internal void Validate(string parameterName)
        {
            if (!Enum.IsDefined(typeof(RuntimeAuthorityImplementationKind), Kind))
            {
                throw new ArgumentException(
                    "A runtime-authority implementation requires a valid " +
                    "origin.",
                    parameterName);
            }

            bool hasExternalIntegration =
                DeclaredExternalIntegrationId.HasValue;
            if (Kind == RuntimeAuthorityImplementationKind.KleiBaseline)
            {
                if (hasExternalIntegration)
                {
                    throw new ArgumentException(
                        "The Klei baseline cannot carry a declared external " +
                        "integration identity.",
                        parameterName);
                }

                return;
            }

            if (!hasExternalIntegration ||
                string.IsNullOrEmpty(
                    DeclaredExternalIntegrationId.GetValueOrDefault().Value))
            {
                throw new ArgumentException(
                    "A declared external runtime-authority implementation " +
                    "requires its integration identity.",
                    parameterName);
            }
        }

        public bool Equals(RuntimeAuthorityImplementationIdentity other) =>
            Kind == other.Kind &&
            Nullable.Equals(
                DeclaredExternalIntegrationId,
                other.DeclaredExternalIntegrationId);

        public override bool Equals(object? obj) =>
            obj is RuntimeAuthorityImplementationIdentity other &&
            Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Kind * 397) ^
                    (DeclaredExternalIntegrationId?.GetHashCode() ?? 0);
            }
        }

        public override string ToString() =>
            Kind == RuntimeAuthorityImplementationKind.KleiBaseline
                ? "klei-baseline"
                : DeclaredExternalIntegrationId.HasValue
                    ? "declared-external-integration:" +
                        DeclaredExternalIntegrationId.Value.Value
                    : string.Empty;
    }
}
