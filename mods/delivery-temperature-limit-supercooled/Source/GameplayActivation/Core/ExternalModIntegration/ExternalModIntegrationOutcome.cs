#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DeliveryTemperatureLimit
{
    internal sealed class ExternalModIntegrationDiagnostic
    {
        internal ExternalModIntegrationDiagnostic(string code, string message)
        {
            Code = ExternalModIntegrationModelValidation.RequireDiagnosticCode(
                code,
                nameof(code));
            Message = ExternalModIntegrationModelValidation
                .RequireDiagnosticMessage(message, nameof(message));
        }

        internal string Code { get; }

        internal string Message { get; }
    }

    internal sealed class ExternalModIntegrationCapabilityOutcome
    {
        internal ExternalModIntegrationCapabilityOutcome(
            RuntimeCapabilityId capabilityId,
            ExternalModIntegrationCategory category,
            RuntimeAuthorityObservation authorityObservation,
            IntegrationContractState contractState,
            IntegrationCapabilityDisposition disposition,
            string? diagnosticCode,
            string? diagnosticMessage)
        {
            ExternalModIntegrationModelValidation.RequireCapabilityId(
                capabilityId,
                nameof(capabilityId));
            ExternalModIntegrationModelValidation.RequireDefinedEnum(
                category,
                nameof(category));
            ExternalModIntegrationModelValidation.RequireDefinedEnum(
                authorityObservation,
                nameof(authorityObservation));
            ExternalModIntegrationModelValidation.RequireDefinedEnum(
                contractState,
                nameof(contractState));
            ExternalModIntegrationModelValidation.RequireDefinedEnum(
                disposition,
                nameof(disposition));
            ExternalModIntegrationModelValidation.ValidateOptionalDiagnostic(
                diagnosticCode,
                diagnosticMessage,
                nameof(diagnosticCode),
                nameof(diagnosticMessage));

            bool diagnosticRequired =
                authorityObservation ==
                    RuntimeAuthorityObservation.OwnsIncompatible ||
                authorityObservation ==
                    RuntimeAuthorityObservation.OwnershipUnavailable ||
                contractState == IntegrationContractState.Incompatible ||
                contractState ==
                    IntegrationContractState.VerificationUnavailable ||
                disposition ==
                    IntegrationCapabilityDisposition.Unavailable ||
                disposition ==
                    IntegrationCapabilityDisposition.ActivationBlocking;
            if (diagnosticRequired && diagnosticCode == null)
            {
                throw new ArgumentException(
                    "An unavailable, incompatible, or activation-blocking " +
                    "capability outcome requires a stable diagnostic.");
            }

            CapabilityId = capabilityId;
            Category = category;
            AuthorityObservation = authorityObservation;
            ContractState = contractState;
            Disposition = disposition;
            DiagnosticCode = diagnosticCode;
            DiagnosticMessage = diagnosticMessage;
        }

        internal RuntimeCapabilityId CapabilityId { get; }

        internal ExternalModIntegrationCategory Category { get; }

        internal RuntimeAuthorityObservation AuthorityObservation { get; }

        internal IntegrationContractState ContractState { get; }

        internal IntegrationCapabilityDisposition Disposition { get; }

        internal string? DiagnosticCode { get; }

        internal string? DiagnosticMessage { get; }
    }

    /// <summary>
    /// Sanitized provider-neutral result retained beyond the short-lived loaded
    /// mod inspection context. It owns bounded scalar facts only.
    /// </summary>
    internal sealed class ExternalModIntegrationOutcome
    {
        internal ExternalModIntegrationOutcome(
            DeclaredModIntegrationId integrationId,
            string displayName,
            IEnumerable<ExternalModIntegrationCategory> categories,
            DeclaredModMatchState matchState,
            string? assemblyIdentity,
            string? assemblyVersion,
            string? fileVersion,
            string? assemblySha256,
            IEnumerable<ExternalModIntegrationCapabilityOutcome> capabilities,
            IEnumerable<ExternalModIntegrationDiagnostic> diagnostics)
        {
            ExternalModIntegrationModelValidation.RequireIntegrationId(
                integrationId,
                nameof(integrationId));
            IntegrationId = integrationId;
            DisplayName = ExternalModIntegrationModelValidation
                .RequireDisplayName(displayName, nameof(displayName));
            Categories = ExternalModIntegrationModelValidation.CopyCategories(
                categories,
                nameof(categories));
            ExternalModIntegrationModelValidation.RequireDefinedEnum(
                matchState,
                nameof(matchState));
            MatchState = matchState;
            AssemblyIdentity = ExternalModIntegrationModelValidation
                .RequireOptionalBoundedScalar(
                    assemblyIdentity,
                    nameof(assemblyIdentity),
                    512);
            AssemblyVersion = ExternalModIntegrationModelValidation
                .RequireOptionalBoundedScalar(
                    assemblyVersion,
                    nameof(assemblyVersion),
                    128);
            FileVersion = ExternalModIntegrationModelValidation
                .RequireOptionalBoundedScalar(
                    fileVersion,
                    nameof(fileVersion),
                    128);
            AssemblySha256 = ExternalModIntegrationModelValidation
                .RequireOptionalSha256(assemblySha256, nameof(assemblySha256));
            Capabilities = CopyCapabilities(capabilities, Categories);
            Diagnostics = CopyDiagnostics(diagnostics);
        }

        internal DeclaredModIntegrationId IntegrationId { get; }

        internal string DisplayName { get; }

        internal IReadOnlyList<ExternalModIntegrationCategory> Categories { get; }

        internal DeclaredModMatchState MatchState { get; }

        internal string? AssemblyIdentity { get; }

        internal string? AssemblyVersion { get; }

        internal string? FileVersion { get; }

        internal string? AssemblySha256 { get; }

        internal IReadOnlyList<ExternalModIntegrationCapabilityOutcome>
            Capabilities { get; }

        internal IReadOnlyList<ExternalModIntegrationDiagnostic>
            Diagnostics { get; }

        private static IReadOnlyList<ExternalModIntegrationCapabilityOutcome>
            CopyCapabilities(
                IEnumerable<ExternalModIntegrationCapabilityOutcome>
                    capabilities,
                IReadOnlyList<ExternalModIntegrationCategory> categories)
        {
            if (capabilities == null)
            {
                throw new ArgumentNullException(nameof(capabilities));
            }

            var copied = new List<ExternalModIntegrationCapabilityOutcome>();
            var seen = new HashSet<RuntimeCapabilityId>();
            foreach (ExternalModIntegrationCapabilityOutcome capability in
                     capabilities)
            {
                if (capability == null)
                {
                    throw new ArgumentException(
                        "An external-mod capability outcome cannot be null.",
                        nameof(capabilities));
                }

                if (!seen.Add(capability.CapabilityId))
                {
                    throw new ArgumentException(
                        "An integration outcome cannot repeat a capability.",
                        nameof(capabilities));
                }

                if (!ContainsCategory(categories, capability.Category))
                {
                    throw new ArgumentException(
                        "An integration capability outcome must retain one of " +
                        "the integration outcome's declared categories.",
                        nameof(capabilities));
                }

                copied.Add(capability);
            }

            return new ReadOnlyCollection<
                ExternalModIntegrationCapabilityOutcome>(copied);
        }

        private static bool ContainsCategory(
            IReadOnlyList<ExternalModIntegrationCategory> categories,
            ExternalModIntegrationCategory candidate)
        {
            for (int index = 0; index < categories.Count; index++)
            {
                if (categories[index] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<ExternalModIntegrationDiagnostic>
            CopyDiagnostics(
                IEnumerable<ExternalModIntegrationDiagnostic> diagnostics)
        {
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            var copied = new List<ExternalModIntegrationDiagnostic>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (ExternalModIntegrationDiagnostic diagnostic in diagnostics)
            {
                if (diagnostic == null)
                {
                    throw new ArgumentException(
                        "An external-mod diagnostic cannot be null.",
                        nameof(diagnostics));
                }

                if (!seen.Add(diagnostic.Code))
                {
                    throw new ArgumentException(
                        "An integration outcome cannot repeat a diagnostic code.",
                        nameof(diagnostics));
                }

                copied.Add(diagnostic);
            }

            return new ReadOnlyCollection<ExternalModIntegrationDiagnostic>(
                copied);
        }
    }

    internal static class ExternalModIntegrationModelValidation
    {
        internal const int MaximumDisplayNameCharacters = 128;
        internal const int MaximumDiagnosticMessageCharacters = 2048;

        internal static void RequireIntegrationId(
            DeclaredModIntegrationId integrationId,
            string parameterName)
        {
            if (string.IsNullOrEmpty(integrationId.Value))
            {
                throw new ArgumentException(
                    "A declared integration requires a valid identity.",
                    parameterName);
            }
        }

        internal static void RequireCapabilityId(
            RuntimeCapabilityId capabilityId,
            string parameterName)
        {
            if (string.IsNullOrEmpty(capabilityId.Value))
            {
                throw new ArgumentException(
                    "An integration capability requires a valid identity.",
                    parameterName);
            }
        }

        internal static TEnum RequireDefinedEnum<TEnum>(
            TEnum value,
            string parameterName)
            where TEnum : struct
        {
            if (!Enum.IsDefined(typeof(TEnum), value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Unknown integration model state.");
            }

            return value;
        }

        internal static string RequireDisplayName(
            string value,
            string parameterName) =>
            RequireExactBoundedText(
                value,
                parameterName,
                MaximumDisplayNameCharacters,
                "An integration display name");

        internal static string RequireExactBoundedText(
            string? value,
            string parameterName,
            int maximumCharacters,
            string semanticSubject)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value) ||
                value.Length > maximumCharacters ||
                !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    semanticSubject +
                    " must be non-blank, bounded, and free of surrounding " +
                    "whitespace.",
                    parameterName);
            }

            return value;
        }

        internal static string RequireDiagnosticCode(
            string value,
            string parameterName) =>
            ValidatedIntegrationIdentifier.RequireKebabCase(
                value,
                parameterName);

        internal static string RequireDiagnosticMessage(
            string value,
            string parameterName) =>
            RequireExactBoundedText(
                value,
                parameterName,
                MaximumDiagnosticMessageCharacters,
                "A diagnostic message");

        internal static void ValidateOptionalDiagnostic(
            string? code,
            string? message,
            string codeParameterName,
            string messageParameterName)
        {
            if ((code == null) != (message == null))
            {
                throw new ArgumentException(
                    "A diagnostic code and message must be supplied together.");
            }

            if (code != null)
            {
                RequireDiagnosticCode(code, codeParameterName);
                RequireDiagnosticMessage(message!, messageParameterName);
            }
        }

        internal static IReadOnlyList<ExternalModIntegrationCategory>
            CopyCategories(
                IEnumerable<ExternalModIntegrationCategory> categories,
                string parameterName)
        {
            if (categories == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copied = new List<ExternalModIntegrationCategory>();
            var seen = new HashSet<ExternalModIntegrationCategory>();
            foreach (ExternalModIntegrationCategory category in categories)
            {
                RequireDefinedEnum(category, parameterName);
                if (!seen.Add(category))
                {
                    throw new ArgumentException(
                        "An integration cannot repeat a category.",
                        parameterName);
                }

                copied.Add(category);
            }

            if (copied.Count == 0)
            {
                throw new ArgumentException(
                    "An integration requires at least one category.",
                    parameterName);
            }

            copied.Sort();
            return new ReadOnlyCollection<ExternalModIntegrationCategory>(
                copied);
        }

        internal static string? RequireOptionalBoundedScalar(
            string? value,
            string parameterName,
            int maximumCharacters)
        {
            if (value == null)
            {
                return null;
            }

            return RequireExactBoundedText(
                value,
                parameterName,
                maximumCharacters,
                "An integration fact");
        }

        internal static string? RequireOptionalSha256(
            string? value,
            string parameterName)
        {
            if (value == null)
            {
                return null;
            }

            if (value.Length != 64)
            {
                throw new ArgumentException(
                    "An assembly SHA-256 fact must contain exactly 64 uppercase " +
                    "hexadecimal characters.",
                    parameterName);
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool isDigit = character >= '0' && character <= '9';
                bool isUpperHex = character >= 'A' && character <= 'F';
                if (!isDigit && !isUpperHex)
                {
                    throw new ArgumentException(
                        "An assembly SHA-256 fact must contain exactly 64 " +
                        "uppercase hexadecimal characters.",
                        parameterName);
                }
            }

            return value;
        }
    }
}
